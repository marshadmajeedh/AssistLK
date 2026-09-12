<#
.SYNOPSIS
    AssistLK Component 1 Local Development Launcher
    Starts the Python Problem Understanding Agent and ASP.NET Core in a single command.

.DESCRIPTION
    1. Validates Python virtual environment (.venv) and ASP.NET Core paths.
    2. Probes port 8001 for an existing healthy AssistLK Python agent instance.
    3. If not already running, starts the Python agent using the venv executable.
    4. Confirms Python /health reports HTTP 200 within a bounded timeout.
    5. Configures process-level environment variables for Python agent service.
    6. Starts ASP.NET Core backend.
    7. Cleanly terminates only processes started by this script upon Ctrl+C or exit.

.EXAMPLE
    .\scripts\start-c1-dev.ps1
#>

[CmdletBinding()]
param(
    [int]$HealthTimeoutSeconds = 20,
    [int]$HealthPollIntervalMs = 500,
    [string]$PythonHost = "127.0.0.1",
    [int]$PythonPort = 8001,
    [switch]$NoDotnet
)

$ErrorActionPreference = "Continue"

# -------------------------------------------------------------
# Helper: Terminate process and its children safely
# -------------------------------------------------------------
function Stop-ProcessTree {
    param([int]$ProcessId)
    if ($ProcessId -le 0) { return }

    # Attempt tree termination with Windows taskkill
    try {
        $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($p -and -not $p.HasExited) {
            & taskkill /pid $ProcessId /t /f 2>$null | Out-Null
        }
    } catch { }

    # Fallback verification for any direct child processes
    try {
        $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $ProcessId" -ErrorAction SilentlyContinue
        foreach ($child in $children) {
            Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    } catch { }
}

# -------------------------------------------------------------
# Helper: Test if TCP port is active
# -------------------------------------------------------------
function Test-PortInUse {
    param(
        [string]$Address = "127.0.0.1",
        [int]$Port = 8001
    )
    $tcp = New-Object System.Net.Sockets.TcpClient
    try {
        $asyncResult = $tcp.BeginConnect($Address, $Port, $null, $null)
        $connected = $asyncResult.AsyncWaitHandle.WaitOne(800, $false)
        if ($connected -and $tcp.Connected) {
            $tcp.EndConnect($asyncResult)
            return $true
        }
    } catch {
        # Connection refused or host unreachable
    } finally {
        $tcp.Close()
        $tcp.Dispose()
    }

    try {
        $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        if ($conn) { return $true }
    } catch { }

    return $false
}

# -------------------------------------------------------------
# Helper: Probe /health on Python Agent
# -------------------------------------------------------------
function Get-AgentHealth {
    param([string]$Url)
    try {
        $res = Invoke-RestMethod -Uri $Url -Method Get -TimeoutSec 3 -ErrorAction Stop
        $isExpected = ($null -ne $res -and $res.status -eq "healthy" -and $res.service -eq "problem-understanding-agent")
        return @{
            IsSuccess = $true
            IsExpectedAgent = $isExpected
            Data = $res
            Error = $null
        }
    } catch {
        return @{
            IsSuccess = $false
            IsExpectedAgent = $false
            Data = $null
            Error = $_.Exception.Message
        }
    }
}

# -------------------------------------------------------------
# 1. Resolve Repository Root & Validate Paths
# -------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..")).Path

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " AssistLK Component 1 Development Launcher" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Repository root: $RepoRoot" -ForegroundColor DarkGray

$PythonAgentDir = Join-Path $RepoRoot "agent-services\problem-understanding-agent"
$VenvDir = Join-Path $PythonAgentDir ".venv"
$PythonExe = Join-Path $VenvDir "Scripts\python.exe"
$ApiProjectPath = Join-Path $RepoRoot "backend\src\AssistLK.Api"

# Verify Python service directory
if (-not (Test-Path $PythonAgentDir -PathType Container)) {
    Write-Error "Python agent directory not found at: $PythonAgentDir"
    exit 1
}

# Verify virtual environment
if (-not (Test-Path $VenvDir -PathType Container)) {
    Write-Host "`n[ERROR] Python virtual environment not found at: $VenvDir" -ForegroundColor Red
    Write-Host "First-time setup is required before launching:" -ForegroundColor Yellow
    Write-Host "  cd agent-services/problem-understanding-agent" -ForegroundColor Yellow
    Write-Host "  python -m venv .venv" -ForegroundColor Yellow
    Write-Host "  .\.venv\Scripts\Activate.ps1" -ForegroundColor Yellow
    Write-Host "  pip install -r requirements.txt" -ForegroundColor Yellow
    exit 1
}

# Verify Python executable
if (-not (Test-Path $PythonExe -PathType Leaf)) {
    Write-Host "`n[ERROR] Python executable not found at: $PythonExe" -ForegroundColor Red
    Write-Host "Ensure the virtual environment is intact or recreate it." -ForegroundColor Yellow
    exit 1
}

# Verify ASP.NET project directory
if (-not (Test-Path $ApiProjectPath -PathType Container)) {
    Write-Error "ASP.NET project directory not found at: $ApiProjectPath"
    exit 1
}

# -------------------------------------------------------------
# 2. Check Existing Instance & Port Conflict Safety
# -------------------------------------------------------------
$healthUrl = "http://${PythonHost}:${PythonPort}/health"
$portOccupied = Test-PortInUse -Address $PythonHost -Port $PythonPort

$pythonProcess = $null
$scriptStartedPython = $false

if ($portOccupied) {
    Write-Host "Port ${PythonPort} is in use; checking /health for existing AssistLK agent..." -ForegroundColor DarkGray
    $initialHealth = Get-AgentHealth -Url $healthUrl

    if ($initialHealth.IsSuccess -and $initialHealth.IsExpectedAgent) {
        Write-Host "AssistLK Python agent is already running on port ${PythonPort}." -ForegroundColor Green
        Write-Host "  Provider: $($initialHealth.Data.provider) | Model: $($initialHealth.Data.model)" -ForegroundColor DarkGray
        # Existing instance detected; will not kill on exit
        $scriptStartedPython = $false
    } else {
        Write-Host "`n[ERROR] Port ${PythonPort} is occupied, but ${healthUrl} did not identify the expected AssistLK problem-understanding-agent service." -ForegroundColor Red
        Write-Host "[SAFETY STOP] Unrecognized process detected on port ${PythonPort}. The process will NOT be terminated." -ForegroundColor Yellow
        Write-Host "Please close the conflicting application or configure another port before retrying.`n" -ForegroundColor Yellow
        exit 1
    }
} else {
    # ---------------------------------------------------------
    # 3. Start Python Agent in Virtual Environment
    # ---------------------------------------------------------
    Write-Host "`n[1/2] Starting Python Problem Understanding Agent..." -ForegroundColor Cyan
    Write-Host "  Command: python -m uvicorn app.main:app --host $PythonHost --port $PythonPort" -ForegroundColor DarkGray

    $pythonArgs = @("-m", "uvicorn", "app.main:app", "--host", $PythonHost, "--port", $PythonPort.ToString())
    
    $pythonProcess = Start-Process -FilePath $PythonExe `
        -ArgumentList $pythonArgs `
        -WorkingDirectory $PythonAgentDir `
        -PassThru `
        -NoNewWindow

    $scriptStartedPython = $true
    Write-Host "  Spawned Python Agent (PID: $($pythonProcess.Id))." -ForegroundColor DarkGray
    Write-Host "  Waiting for health check at ${healthUrl} (timeout: ${HealthTimeoutSeconds}s)..." -ForegroundColor DarkGray

    # ---------------------------------------------------------
    # 4. Bounded Health Check Polling
    # ---------------------------------------------------------
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $isHealthy = $false

    while ($stopwatch.Elapsed.TotalSeconds -lt $HealthTimeoutSeconds) {
        if ($pythonProcess.HasExited) {
            Write-Host "`n[ERROR] Python agent process exited prematurely (ExitCode: $($pythonProcess.ExitCode))." -ForegroundColor Red
            Stop-ProcessTree -ProcessId $pythonProcess.Id
            exit 1
        }

        $health = Get-AgentHealth -Url $healthUrl
        if ($health.IsSuccess -and $health.IsExpectedAgent) {
            $isHealthy = $true
            $elapsed = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
            Write-Host "  Python Agent is healthy on port ${PythonPort} (${elapsed}s)." -ForegroundColor Green
            Write-Host "    Service:     $($health.Data.service)" -ForegroundColor DarkGray
            Write-Host "    Provider:    $($health.Data.provider)" -ForegroundColor DarkGray
            Write-Host "    Model:       $($health.Data.model)" -ForegroundColor DarkGray
            Write-Host "    Environment: $($health.Data.environment)" -ForegroundColor DarkGray
            break
        }

        Start-Sleep -Milliseconds $HealthPollIntervalMs
    }

    if (-not $isHealthy) {
        Write-Host "`n[ERROR] Python agent failed to report healthy status within ${HealthTimeoutSeconds} seconds." -ForegroundColor Red
        Stop-ProcessTree -ProcessId $pythonProcess.Id
        exit 1
    }
}

if ($NoDotnet) {
    Write-Host "`n-NoDotnet specified. Python agent running. Exiting without starting ASP.NET.`n" -ForegroundColor Yellow
    exit 0
}

# -------------------------------------------------------------
# 5. Configure ASP.NET Process Environment
# -------------------------------------------------------------
Write-Host "`n[2/2] Configuring ASP.NET environment for Python agent service..." -ForegroundColor Cyan

$env:AgentServices__ProblemUnderstandingUrl = "http://${PythonHost}:${PythonPort}"

Write-Host "  AgentServices__ProblemUnderstandingUrl  = http://${PythonHost}:${PythonPort}" -ForegroundColor Green

if (-not [string]::IsNullOrWhiteSpace($env:AgentServices__InternalApiKey)) {
    Write-Host "  AgentServices__InternalApiKey           = [CONFIGURED IN ENVIRONMENT]" -ForegroundColor Green
} else {
    Write-Host "  AgentServices__InternalApiKey           = [NOT SET / DEV OPEN]" -ForegroundColor DarkGray
}

# -------------------------------------------------------------
# 6. Start ASP.NET Core Backend & Manage Lifecycle
# -------------------------------------------------------------
$dotnetProcess = $null

try {
    Write-Host "`nStarting ASP.NET Core backend (dotnet run --project backend/src/AssistLK.Api)..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to shut down both ASP.NET Core and the Python Agent.`n" -ForegroundColor Yellow

    $dotnetProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--project", "backend/src/AssistLK.Api" `
        -WorkingDirectory $RepoRoot `
        -PassThru `
        -NoNewWindow

    Write-Host "ASP.NET Core started (PID: $($dotnetProcess.Id)).`n" -ForegroundColor DarkGray

    # Wait for process exit or interrupt signal
    while (-not $dotnetProcess.HasExited) {
        Start-Sleep -Milliseconds 250
    }
}
catch [System.Management.Automation.PipelineStoppedException] {
    Write-Host "`n[Launcher] Interrupt signal received (Ctrl+C). Initiating shutdown..." -ForegroundColor Yellow
}
catch {
    Write-Host "`n[Launcher] Execution interrupted: $($_.Exception.Message)" -ForegroundColor Yellow
}
finally {
    Write-Host "`n[Launcher] Initiating clean process shutdown..." -ForegroundColor Cyan

    # Stop ASP.NET if started by this script
    if ($dotnetProcess -and -not $dotnetProcess.HasExited) {
        Write-Host "  Stopping ASP.NET Core (PID: $($dotnetProcess.Id))..." -ForegroundColor DarkGray
        Stop-ProcessTree -ProcessId $dotnetProcess.Id
    }

    # Stop Python only if started by this script
    if ($scriptStartedPython -and $pythonProcess -and -not $pythonProcess.HasExited) {
        Write-Host "  Stopping Python Agent (PID: $($pythonProcess.Id))..." -ForegroundColor DarkGray
        Stop-ProcessTree -ProcessId $pythonProcess.Id
    } elseif (-not $scriptStartedPython) {
        Write-Host "  Python Agent was already running prior to launch; leaving it running." -ForegroundColor DarkGray
    }

    Write-Host "[Launcher] All processes managed by this session have been cleanly terminated.`n" -ForegroundColor Green
}
