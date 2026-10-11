<#
.SYNOPSIS
    AssistLK Integrated Local Development Stop Helper
    Safely terminates lingering AssistLK development processes (C2:8000, C1:8001, API:5012)
    while strictly protecting unrecognized processes.

.DESCRIPTION
    1. Verifies port 8000 is running the AssistLK Provider Matching Agent before stopping.
    2. Verifies port 8001 is running the AssistLK Problem Understanding Agent before stopping.
    3. Verifies port 5012 is running the AssistLK.Api dotnet process before stopping.
    4. Leaves all unrecognized processes untouched.

.EXAMPLE
    .\scripts\stop-integrated-dev.ps1
#>

[CmdletBinding()]
param(
    [int]$C2Port = 8000,
    [int]$C1Port = 8001,
    [int]$ApiPort = 5012
)

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " AssistLK Integrated Development Process Stopper" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

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

# 1. Stop C2 Provider Matching Agent if identified
$c2Url = "http://127.0.0.1:${C2Port}/openapi.json"
try {
    $res = Invoke-RestMethod -Uri $c2Url -Method Get -TimeoutSec 2 -ErrorAction Stop
    if ($res -and $res.info -and $res.info.title -like "*Provider Matching*") {
        $conn = Get-NetTCPConnection -LocalPort $C2Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn -and $conn.OwningProcess) {
            Write-Host "Found AssistLK C2 Provider Matching Agent on port ${C2Port} (PID: $($conn.OwningProcess)). Stopping..." -ForegroundColor Yellow
            Stop-ProcessTree -ProcessId $conn.OwningProcess
            Write-Host "  C2 Agent stopped." -ForegroundColor Green
        }
    } else {
        Write-Host "Port ${C2Port} is in use, but does not identify as AssistLK Provider Matching Agent. Leaving untouched." -ForegroundColor DarkGray
    }
} catch {
    Write-Host "No active AssistLK C2 Agent detected on port ${C2Port}." -ForegroundColor DarkGray
}

# 2. Stop C1 Problem Understanding Agent if identified
$c1Url = "http://127.0.0.1:${C1Port}/health"
try {
    $res = Invoke-RestMethod -Uri $c1Url -Method Get -TimeoutSec 2 -ErrorAction Stop
    if ($res -and $res.status -eq "healthy" -and $res.service -eq "problem-understanding-agent") {
        $conn = Get-NetTCPConnection -LocalPort $C1Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn -and $conn.OwningProcess) {
            Write-Host "Found AssistLK C1 Problem Understanding Agent on port ${C1Port} (PID: $($conn.OwningProcess)). Stopping..." -ForegroundColor Yellow
            Stop-ProcessTree -ProcessId $conn.OwningProcess
            Write-Host "  C1 Agent stopped." -ForegroundColor Green
        }
    } else {
        Write-Host "Port ${C1Port} is in use, but does not identify as AssistLK Problem Understanding Agent. Leaving untouched." -ForegroundColor DarkGray
    }
} catch {
    Write-Host "No active AssistLK C1 Agent detected on port ${C1Port}." -ForegroundColor DarkGray
}

# 3. Stop ASP.NET Core API on port 5012 if identified
try {
    $conn = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($conn -and $conn.OwningProcess) {
        $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
        $cim = Get-CimInstance Win32_Process -Filter "ProcessId = $($conn.OwningProcess)" -ErrorAction SilentlyContinue
        if ($proc -and ($proc.ProcessName -match "AssistLK|dotnet" -or ($cim -and $cim.CommandLine -like "*AssistLK*"))) {
            Write-Host "Found AssistLK API on port ${ApiPort} (PID: $($conn.OwningProcess), Name: $($proc.ProcessName)). Stopping..." -ForegroundColor Yellow
            Stop-ProcessTree -ProcessId $conn.OwningProcess
            Write-Host "  ASP.NET Core API stopped." -ForegroundColor Green
        } else {
            Write-Host "Port ${ApiPort} is in use, but does not identify as AssistLK API. Leaving untouched." -ForegroundColor DarkGray
        }
    } else {
        Write-Host "No active AssistLK API detected on port ${ApiPort}." -ForegroundColor DarkGray
    }
} catch {
    Write-Host "No active AssistLK API detected on port ${ApiPort}." -ForegroundColor DarkGray
}

Write-Host "Stop operation complete.`n" -ForegroundColor Green
