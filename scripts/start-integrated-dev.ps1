<#
.SYNOPSIS
    AssistLK Integrated Local Development Launcher
    Starts C2 (Provider Matching Agent), C1 (Problem Understanding Agent),
    and ASP.NET Core in strict dependency order with readiness verification.

.DESCRIPTION
    1. Resolves repository root and validates Python venvs and ASP.NET project paths.
    2. Validates environment variables (GOOGLE_API_KEY, AgentServices__InternalApiKey,
       INTERNAL_API_KEY) without leaking secrets.
    3. Checks port occupancy on 8000, 8001, and 5012 with unknown-process safety stops.
    4. Starts C2 (Provider Matching Agent, port 8000) FIRST.
    5. Probes C2 /openapi.json until /match/start is confirmed ready within timeout.
    6. Starts C1 (Problem Understanding Agent, port 8001) SECOND.
    7. Probes C1 /health until healthy status is confirmed within timeout.
    8. Starts ASP.NET Core backend (port 5012) only after both agents are verified ready,
       preventing ProviderMatchingBackgroundWorker startup connection refusal (10061).
    9. Cleanly terminates processes started by this session upon Ctrl+C or script exit.

.EXAMPLE
    .\scripts\start-integrated-dev.ps1
    .\scripts\start-integrated-dev.ps1 -NoDotnet
    .\scripts\start-integrated-dev.ps1 -ReuseAgents
#>

[CmdletBinding()]
param(
    [int]$HealthTimeoutSeconds = 30,
    [int]$HealthPollIntervalMs = 500,
    [string]$C2Host = "127.0.0.1",
    [int]$C2Port = 8000,
    [string]$C1Host = "127.0.0.1",
    [int]$C1Port = 8001,
    [int]$ApiPort = 5012,
    [switch]$NoDotnet,
    [switch]$ReuseAgents,
    [switch]$ReuseC1,
    [switch]$ReuseC2,
    [string]$RootEnvPath,
    [string]$C1EnvPath
)

$ErrorActionPreference = "Continue"

# -------------------------------------------------------------
# Helper: Terminate process and its children safely
# -------------------------------------------------------------
function Stop-ProcessTree {
    param([int]$ProcessId)
    if ($ProcessId -le 0) { return }

    try {
        $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($p -and -not $p.HasExited) {
            & taskkill /pid $ProcessId /t /f 2>$null | Out-Null
        }
    } catch { }

    try {
        $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $ProcessId" -ErrorAction SilentlyContinue
        foreach ($child in $children) {
            Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    } catch { }
}

# -------------------------------------------------------------
# Helper: Test if TCP port is actively listening
# -------------------------------------------------------------
function Test-PortInUse {
    param(
        [string]$Address = "127.0.0.1",
        [int]$Port
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
# Helper: Get process details owning a port
# -------------------------------------------------------------
function Get-PortProcessDetails {
    param([int]$Port)
    try {
        $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn -and $conn.OwningProcess) {
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            $cim = Get-CimInstance Win32_Process -Filter "ProcessId = $($conn.OwningProcess)" -ErrorAction SilentlyContinue
            return @{
                PID = $conn.OwningProcess
                ProcessName = if ($proc) { $proc.ProcessName } else { "Unknown" }
                CommandLine = if ($cim) { $cim.CommandLine } else { "" }
            }
        }
    } catch { }
    return $null
}

# -------------------------------------------------------------
# Helper: Probe /openapi.json on C2 Provider Matching Agent
# -------------------------------------------------------------
function Get-C2Readiness {
    param([string]$Url)
    try {
        $res = Invoke-RestMethod -Uri $Url -Method Get -TimeoutSec 3 -ErrorAction Stop
        $hasTitle = ($null -ne $res -and $null -ne $res.info -and $res.info.title -like "*Provider Matching*")
        $hasStart = ($null -ne $res -and $null -ne $res.paths -and ($res.paths.PSObject.Properties.Name -contains "/match/start"))
        $hasResume = ($null -ne $res -and $null -ne $res.paths -and ($res.paths.PSObject.Properties.Name -contains "/match/resume"))

        return @{
            IsSuccess = $true
            IsExpectedAgent = ($hasTitle -and $hasStart)
            HasMatchStart = $hasStart
            HasMatchResume = $hasResume
            Title = if ($res.info) { $res.info.title } else { $null }
            Data = $res
            Error = $null
        }
    } catch {
        return @{
            IsSuccess = $false
            IsExpectedAgent = $false
            HasMatchStart = $false
            HasMatchResume = $false
            Title = $null
            Data = $null
            Error = $_.Exception.Message
        }
    }
}

# -------------------------------------------------------------
# Helper: Probe /health on C1 Problem Understanding Agent
# -------------------------------------------------------------
function Get-C1Health {
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
# Helper: Parse key value safely from a .env file
# -------------------------------------------------------------
function Get-EnvFileKeyValue {
    param(
        [string]$FilePath,
        [string]$Key
    )
    if (-not (Test-Path $FilePath -PathType Leaf)) { return $null }
    try {
        $lines = Get-Content -Path $FilePath -ErrorAction Stop
        foreach ($line in $lines) {
            $trimmed = $line.Trim()
            if ($trimmed.StartsWith("#") -or -not ($trimmed.Contains("="))) { continue }
            $parts = $trimmed.Split("=", 2)
            if ($parts[0].Trim() -eq $Key) {
                $val = $parts[1].Trim()
                if (($val.StartsWith('"') -and $val.EndsWith('"')) -or ($val.StartsWith("'") -and $val.EndsWith("'"))) {
                    if ($val.Length -ge 2) {
                        $val = $val.Substring(1, $val.Length - 2).Trim()
                    }
                }
                return $val
            }
        }
    } catch { }
    return $null
}

# -------------------------------------------------------------
# Helper: Compute safe 8-character SHA-256 fingerprint for logging
# -------------------------------------------------------------
function Get-KeyFingerprint {
    param([string]$Key)
    if ([string]::IsNullOrWhiteSpace($Key)) { return $null }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Key)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $hashBytes = $sha256.ComputeHash($bytes)
    $hex = -join ($hashBytes | ForEach-Object { "{0:X2}" -f $_ })
    return $hex.Substring(0, 8)
}

# -------------------------------------------------------------
# 1. Resolve Repository Root & Validate Paths
# -------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..")).Path

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " AssistLK Integrated Local Development Launcher" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Repository root: $RepoRoot" -ForegroundColor DarkGray

$C2Dir = Join-Path $RepoRoot "agent-services\provider-matching-agent"
$C2VenvDir = Join-Path $C2Dir ".venv"
$C2PythonExe = Join-Path $C2VenvDir "Scripts\python.exe"

$C1Dir = Join-Path $RepoRoot "agent-services\problem-understanding-agent"
$C1VenvDir = Join-Path $C1Dir ".venv"
$C1PythonExe = Join-Path $C1VenvDir "Scripts\python.exe"

$ApiProjectPath = Join-Path $RepoRoot "backend\src\AssistLK.Api"

# Validate C2 directory & venv
if (-not (Test-Path $C2Dir -PathType Container)) {
    Write-Host "`n[ERROR] C2 Provider Matching Agent directory not found: $C2Dir" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $C2PythonExe -PathType Leaf)) {
    Write-Host "`n[ERROR] C2 Python virtual environment not found at: $C2VenvDir" -ForegroundColor Red
    Write-Host "Setup required:" -ForegroundColor Yellow
    Write-Host "  cd agent-services/provider-matching-agent" -ForegroundColor Yellow
    Write-Host "  python -m venv .venv" -ForegroundColor Yellow
    Write-Host "  .\.venv\Scripts\Activate.ps1" -ForegroundColor Yellow
    Write-Host "  pip install -r requirements.txt" -ForegroundColor Yellow
    exit 1
}

# Validate C1 directory & venv
if (-not (Test-Path $C1Dir -PathType Container)) {
    Write-Host "`n[ERROR] C1 Problem Understanding Agent directory not found: $C1Dir" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $C1PythonExe -PathType Leaf)) {
    Write-Host "`n[ERROR] C1 Python virtual environment not found at: $C1VenvDir" -ForegroundColor Red
    Write-Host "Setup required:" -ForegroundColor Yellow
    Write-Host "  cd agent-services/problem-understanding-agent" -ForegroundColor Yellow
    Write-Host "  python -m venv .venv" -ForegroundColor Yellow
    Write-Host "  .\.venv\Scripts\Activate.ps1" -ForegroundColor Yellow
    Write-Host "  pip install -r requirements.txt" -ForegroundColor Yellow
    exit 1
}

# Validate ASP.NET directory
if (-not (Test-Path $ApiProjectPath -PathType Container)) {
    Write-Host "`n[ERROR] ASP.NET project directory not found: $ApiProjectPath" -ForegroundColor Red
    exit 1
}

# -------------------------------------------------------------
# 2. Environment Variables & Secret Validation (Phase 7)
# -------------------------------------------------------------
Write-Host "`nValidating environment configuration (no secrets displayed)..." -ForegroundColor Cyan

$rootEnvFile = if (-not [string]::IsNullOrWhiteSpace($RootEnvPath)) { $RootEnvPath } else { Join-Path $RepoRoot ".env" }
$c1EnvFile   = if (-not [string]::IsNullOrWhiteSpace($C1EnvPath))   { $C1EnvPath }   else { Join-Path $C1Dir ".env" }

# 2a. Resolve GOOGLE_API_KEY
$googleApiKey = if (-not [string]::IsNullOrWhiteSpace($env:GOOGLE_API_KEY)) {
    $env:GOOGLE_API_KEY.Trim().Trim('"', "'")
} else {
    $keyFromRoot = Get-EnvFileKeyValue -FilePath $rootEnvFile -Key "GOOGLE_API_KEY"
    if (-not [string]::IsNullOrWhiteSpace($keyFromRoot)) {
        $keyFromRoot
    } else {
        Get-EnvFileKeyValue -FilePath $c1EnvFile -Key "GOOGLE_API_KEY"
    }
}

if (-not [string]::IsNullOrWhiteSpace($googleApiKey)) {
    $env:GOOGLE_API_KEY = $googleApiKey
    $googleFp = Get-KeyFingerprint $googleApiKey
    Write-Host "  GOOGLE_API_KEY: configured (length: $($googleApiKey.Length), fingerprint: $googleFp)" -ForegroundColor Green
} else {
    Write-Host "  GOOGLE_API_KEY: missing" -ForegroundColor Red
    Write-Host "`n[ERROR] GOOGLE_API_KEY is not configured." -ForegroundColor Red
    Write-Host "C2 Provider Matching Agent requires GOOGLE_API_KEY to initialize Gemini at startup." -ForegroundColor Yellow
    Write-Host "Please set GOOGLE_API_KEY in the environment or in .env.`n" -ForegroundColor Yellow
    exit 1
}

# 2b. Resolve Internal API Keys (C1 <-> ASP.NET)
$aspnetKey = if (-not [string]::IsNullOrWhiteSpace($env:AgentServices__InternalApiKey)) {
    $env:AgentServices__InternalApiKey.Trim().Trim('"', "'")
} else {
    Get-EnvFileKeyValue -FilePath $rootEnvFile -Key "AgentServices__InternalApiKey"
}

$pythonKey = if (-not [string]::IsNullOrWhiteSpace($env:INTERNAL_API_KEY)) {
    $env:INTERNAL_API_KEY.Trim().Trim('"', "'")
} else {
    Get-EnvFileKeyValue -FilePath $c1EnvFile -Key "INTERNAL_API_KEY"
}

$aspConfigured = -not [string]::IsNullOrWhiteSpace($aspnetKey)
$pyConfigured  = -not [string]::IsNullOrWhiteSpace($pythonKey)

$aspFp = if ($aspConfigured) { Get-KeyFingerprint $aspnetKey } else { "none" }
$pyFp  = if ($pyConfigured)  { Get-KeyFingerprint $pythonKey }  else { "none" }
$aspLen = if ($aspConfigured) { $aspnetKey.Length } else { 0 }
$pyLen  = if ($pyConfigured)  { $pythonKey.Length }  else { 0 }

Write-Host "  ASP.NET key:    $(if ($aspConfigured) { "configured (length: $aspLen, fingerprint: $aspFp)" } else { "missing / dev-open" })" -ForegroundColor $(if ($aspConfigured) { "Green" } else { "DarkGray" })
Write-Host "  C1 Python key:  $(if ($pyConfigured)  { "configured (length: $pyLen, fingerprint: $pyFp)" }  else { "missing / dev-open" })" -ForegroundColor $(if ($pyConfigured) { "Green" } else { "DarkGray" })

if ($aspConfigured -ne $pyConfigured) {
    Write-Host "`n[ERROR] Internal service API key configuration mismatch detected!" -ForegroundColor Red
    Write-Host "Both services must use the exact same shared secret." -ForegroundColor Yellow
    exit 1
}

if ($aspConfigured -and $pyConfigured -and ($aspnetKey -ne $pythonKey)) {
    Write-Host "`n[ERROR] Internal service API key mismatch between ASP.NET and C1 Python agent!" -ForegroundColor Red
    exit 1
}

# Set environment for ASP.NET / child processes
$env:AgentServices__ProblemUnderstandingUrl = "http://${C1Host}:${C1Port}"
if ($aspConfigured) {
    $env:AgentServices__InternalApiKey = $aspnetKey
    $env:INTERNAL_API_KEY = $pythonKey
}

# -------------------------------------------------------------
# 3. Port Occupancy & Unknown Process Protection (Phase 6)
# -------------------------------------------------------------
Write-Host "`nChecking ports and existing services (Phase 6 protection)..." -ForegroundColor Cyan

$c2OpenApiUrl = "http://${C2Host}:${C2Port}/openapi.json"
$c1HealthUrl  = "http://${C1Host}:${C1Port}/health"

$scriptStartedC2 = $false
$scriptStartedC1 = $false
$c2Process = $null
$c1Process = $null

# Check C2 Port 8000
$c2PortInUse = Test-PortInUse -Address $C2Host -Port $C2Port
if ($c2PortInUse) {
    $portInfo = Get-PortProcessDetails -Port $C2Port
    $pidStr = if ($portInfo) { "PID: $($portInfo.PID) ($($portInfo.ProcessName))" } else { "Unknown PID" }
    Write-Host "Port ${C2Port} is in use ($pidStr); verifying service identity..." -ForegroundColor DarkGray
    
    $c2Probe = Get-C2Readiness -Url $c2OpenApiUrl
    if ($c2Probe.IsSuccess -and $c2Probe.IsExpectedAgent) {
        if ($ReuseAgents -or $ReuseC2) {
            Write-Host "  Recognized existing AssistLK C2 Provider Matching Agent on port ${C2Port} ($pidStr) [Reusing]." -ForegroundColor Yellow
            $scriptStartedC2 = $false
        } else {
            Write-Host "  Recognized existing AssistLK C2 agent on port ${C2Port} ($pidStr). Restarting for fresh session..." -ForegroundColor Yellow
            if ($portInfo -and $portInfo.PID) {
                Stop-ProcessTree -ProcessId $portInfo.PID
            }
            Start-Sleep -Milliseconds 600
            if (Test-PortInUse -Address $C2Host -Port $C2Port) {
                Write-Host "`n[ERROR] Port ${C2Port} remains occupied after stopping stale agent." -ForegroundColor Red
                exit 1
            }
            $c2PortInUse = $false
        }
    } else {
        Write-Host "`n[ERROR] Port ${C2Port} is occupied, but did not match AssistLK Provider Matching Agent." -ForegroundColor Red
        Write-Host "  Port occupant details: $pidStr" -ForegroundColor Red
        Write-Host "PORT OCCUPIED BY UNKNOWN PROCESS" -ForegroundColor Red
        Write-Host "[SAFETY STOP] Unrecognized process detected on port ${C2Port}. The process will NOT be terminated.`n" -ForegroundColor Yellow
        exit 1
    }
}

# Check C1 Port 8001
$c1PortInUse = Test-PortInUse -Address $C1Host -Port $C1Port
if ($c1PortInUse) {
    $portInfo = Get-PortProcessDetails -Port $C1Port
    $pidStr = if ($portInfo) { "PID: $($portInfo.PID) ($($portInfo.ProcessName))" } else { "Unknown PID" }
    Write-Host "Port ${C1Port} is in use ($pidStr); verifying service identity..." -ForegroundColor DarkGray
    
    $c1Probe = Get-C1Health -Url $c1HealthUrl
    if ($c1Probe.IsSuccess -and $c1Probe.IsExpectedAgent) {
        if ($ReuseAgents -or $ReuseC1) {
            Write-Host "  Recognized existing AssistLK C1 Problem Understanding Agent on port ${C1Port} ($pidStr) [Reusing]." -ForegroundColor Yellow
            $scriptStartedC1 = $false
        } else {
            Write-Host "  Recognized existing AssistLK C1 agent on port ${C1Port} ($pidStr). Restarting for fresh session..." -ForegroundColor Yellow
            if ($portInfo -and $portInfo.PID) {
                Stop-ProcessTree -ProcessId $portInfo.PID
            }
            Start-Sleep -Milliseconds 600
            if (Test-PortInUse -Address $C1Host -Port $C1Port) {
                Write-Host "`n[ERROR] Port ${C1Port} remains occupied after stopping stale agent." -ForegroundColor Red
                exit 1
            }
            $c1PortInUse = $false
        }
    } else {
        Write-Host "`n[ERROR] Port ${C1Port} is occupied, but did not match AssistLK Problem Understanding Agent." -ForegroundColor Red
        Write-Host "  Port occupant details: $pidStr" -ForegroundColor Red
        Write-Host "PORT OCCUPIED BY UNKNOWN PROCESS" -ForegroundColor Red
        Write-Host "[SAFETY STOP] Unrecognized process detected on port ${C1Port}. The process will NOT be terminated.`n" -ForegroundColor Yellow
        exit 1
    }
}

# Check ASP.NET Port 5012
$apiPortInUse = Test-PortInUse -Address "127.0.0.1" -Port $ApiPort
if ($apiPortInUse) {
    $portInfo = Get-PortProcessDetails -Port $ApiPort
    $pidStr = if ($portInfo) { "PID: $($portInfo.PID) ($($portInfo.ProcessName))" } else { "Unknown PID" }
    
    # Check if process is recognized as dotnet / AssistLK
    if ($portInfo -and ($portInfo.ProcessName -match "AssistLK|dotnet" -or $portInfo.CommandLine -like "*AssistLK*")) {
        Write-Host "Recognized existing AssistLK API process on port ${ApiPort} ($pidStr). Stopping for fresh launch..." -ForegroundColor Yellow
        Stop-ProcessTree -ProcessId $portInfo.PID
        Start-Sleep -Milliseconds 600
    } else {
        Write-Host "`n[ERROR] Port ${ApiPort} is occupied by an unrecognized process." -ForegroundColor Red
        Write-Host "  Port occupant details: $pidStr" -ForegroundColor Red
        Write-Host "PORT OCCUPIED BY UNKNOWN PROCESS" -ForegroundColor Red
        Write-Host "[SAFETY STOP] Unrecognized process detected on port ${ApiPort}. The process will NOT be terminated.`n" -ForegroundColor Yellow
        exit 1
    }
}

# -------------------------------------------------------------
# 4. Start C2 Provider Matching Agent FIRST (Phase 5)
# -------------------------------------------------------------
if (-not $c2PortInUse) {
    Write-Host "`n[1/3] Starting C2 Provider Matching Agent (port ${C2Port})..." -ForegroundColor Cyan
    Write-Host "  Command: python -m uvicorn app.main:app --host $C2Host --port $C2Port" -ForegroundColor DarkGray

    $c2Args = @("-m", "uvicorn", "app.main:app", "--host", $C2Host, "--port", $C2Port.ToString())
    $c2Process = Start-Process -FilePath $C2PythonExe `
        -ArgumentList $c2Args `
        -WorkingDirectory $C2Dir `
        -PassThru `
        -NoNewWindow

    $scriptStartedC2 = $true
    Write-Host "  Spawned C2 Process (PID: $($c2Process.Id))." -ForegroundColor DarkGray
    Write-Host "  Waiting for C2 readiness at ${c2OpenApiUrl} (timeout: ${HealthTimeoutSeconds}s)..." -ForegroundColor DarkGray

    # Bounded Readiness Polling
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $c2Ready = $false

    while ($sw.Elapsed.TotalSeconds -lt $HealthTimeoutSeconds) {
        if ($c2Process.HasExited) {
            Write-Host "`n[ERROR] C2 Provider Matching process exited prematurely (ExitCode: $($c2Process.ExitCode))." -ForegroundColor Red
            Stop-ProcessTree -ProcessId $c2Process.Id
            exit 1
        }

        $c2Probe = Get-C2Readiness -Url $c2OpenApiUrl
        if ($c2Probe.IsSuccess -and $c2Probe.IsExpectedAgent) {
            $c2Ready = $true
            $elapsed = [math]::Round($sw.Elapsed.TotalSeconds, 1)
            Write-Host "  C2 Provider Matching Agent is READY on port ${C2Port} (${elapsed}s)." -ForegroundColor Green
            Write-Host "    Title:        $($c2Probe.Title)" -ForegroundColor DarkGray
            Write-Host "    /match/start: Verified exposed" -ForegroundColor DarkGray
            Write-Host "    /match/resume: Verified exposed" -ForegroundColor DarkGray
            break
        }

        Start-Sleep -Milliseconds $HealthPollIntervalMs
    }

    if (-not $c2Ready) {
        Write-Host "`n[ERROR] C2 Provider Matching Agent failed to become ready within ${HealthTimeoutSeconds}s." -ForegroundColor Red
        if ($c2Process) { Stop-ProcessTree -ProcessId $c2Process.Id }
        exit 1
    }
} else {
    Write-Host "`n[1/3] C2 Provider Matching Agent already running on port ${C2Port}." -ForegroundColor Green
}

# -------------------------------------------------------------
# 5. Start C1 Problem Understanding Agent SECOND (Phase 5)
# -------------------------------------------------------------
if (-not $c1PortInUse) {
    Write-Host "`n[2/3] Starting C1 Problem Understanding Agent (port ${C1Port})..." -ForegroundColor Cyan
    Write-Host "  Command: python -m uvicorn app.main:app --host $C1Host --port $C1Port" -ForegroundColor DarkGray

    $c1Args = @("-m", "uvicorn", "app.main:app", "--host", $C1Host, "--port", $C1Port.ToString())
    $c1Process = Start-Process -FilePath $C1PythonExe `
        -ArgumentList $c1Args `
        -WorkingDirectory $C1Dir `
        -PassThru `
        -NoNewWindow

    $scriptStartedC1 = $true
    Write-Host "  Spawned C1 Process (PID: $($c1Process.Id))." -ForegroundColor DarkGray
    Write-Host "  Waiting for C1 health check at ${c1HealthUrl} (timeout: ${HealthTimeoutSeconds}s)..." -ForegroundColor DarkGray

    # Bounded Health Polling
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $c1Ready = $false

    while ($sw.Elapsed.TotalSeconds -lt $HealthTimeoutSeconds) {
        if ($c1Process.HasExited) {
            Write-Host "`n[ERROR] C1 Problem Understanding process exited prematurely (ExitCode: $($c1Process.ExitCode))." -ForegroundColor Red
            Stop-ProcessTree -ProcessId $c1Process.Id
            if ($scriptStartedC2 -and $c2Process) { Stop-ProcessTree -ProcessId $c2Process.Id }
            exit 1
        }

        $c1Health = Get-C1Health -Url $c1HealthUrl
        if ($c1Health.IsSuccess -and $c1Health.IsExpectedAgent) {
            $c1Ready = $true
            $elapsed = [math]::Round($sw.Elapsed.TotalSeconds, 1)
            Write-Host "  C1 Problem Understanding Agent is HEALTHY on port ${C1Port} (${elapsed}s)." -ForegroundColor Green
            Write-Host "    Service:     $($c1Health.Data.service)" -ForegroundColor DarkGray
            Write-Host "    Provider:    $($c1Health.Data.provider)" -ForegroundColor DarkGray
            Write-Host "    Model:       $($c1Health.Data.model)" -ForegroundColor DarkGray
            break
        }

        Start-Sleep -Milliseconds $HealthPollIntervalMs
    }

    if (-not $c1Ready) {
        Write-Host "`n[ERROR] C1 Problem Understanding Agent failed to become healthy within ${HealthTimeoutSeconds}s." -ForegroundColor Red
        if ($c1Process) { Stop-ProcessTree -ProcessId $c1Process.Id }
        if ($scriptStartedC2 -and $c2Process) { Stop-ProcessTree -ProcessId $c2Process.Id }
        exit 1
    }
} else {
    Write-Host "`n[2/3] C1 Problem Understanding Agent already running on port ${C1Port}." -ForegroundColor Green
}

if ($NoDotnet) {
    Write-Host "`n-NoDotnet specified. Agents are running. Exiting without launching ASP.NET.`n" -ForegroundColor Yellow
    exit 0
}

# -------------------------------------------------------------
# 6. Start ASP.NET Core Backend (Phase 5: Only after C2 & C1 ready)
# -------------------------------------------------------------
Write-Host "`n[3/3] Starting ASP.NET Core backend (port ${ApiPort})..." -ForegroundColor Cyan
Write-Host "  All required agent services (C2:8000, C1:8001) are active and verified." -ForegroundColor Green
Write-Host "  ProviderMatchingBackgroundWorker can now safely dispatch to 127.0.0.1:8000.`n" -ForegroundColor Green

$dotnetProcess = $null

try {
    Write-Host "Launching dotnet run --project backend/src/AssistLK.Api --launch-profile http..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to cleanly stop all started services.`n" -ForegroundColor Yellow

    $dotnetProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--project", "backend/src/AssistLK.Api", "--launch-profile", "http" `
        -WorkingDirectory $RepoRoot `
        -PassThru `
        -NoNewWindow

    Write-Host "ASP.NET Core started (PID: $($dotnetProcess.Id))." -ForegroundColor DarkGray

    # Wait for backend process or interrupt
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

    # Stop C1 only if started by this script
    if ($scriptStartedC1 -and $c1Process -and -not $c1Process.HasExited) {
        Write-Host "  Stopping C1 Agent (PID: $($c1Process.Id))..." -ForegroundColor DarkGray
        Stop-ProcessTree -ProcessId $c1Process.Id
    } elseif (-not $scriptStartedC1) {
        Write-Host "  C1 Agent was already running prior to launch; leaving it running." -ForegroundColor DarkGray
    }

    # Stop C2 only if started by this script
    if ($scriptStartedC2 -and $c2Process -and -not $c2Process.HasExited) {
        Write-Host "  Stopping C2 Agent (PID: $($c2Process.Id))..." -ForegroundColor DarkGray
        Stop-ProcessTree -ProcessId $c2Process.Id
    } elseif (-not $scriptStartedC2) {
        Write-Host "  C2 Agent was already running prior to launch; leaving it running." -ForegroundColor DarkGray
    }

    Write-Host "[Launcher] All processes managed by this session have been cleanly terminated.`n" -ForegroundColor Green
}
