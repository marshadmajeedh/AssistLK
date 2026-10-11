<#
.SYNOPSIS
    AssistLK Component 1 Local Development Stop Helper
    Safely terminates lingering AssistLK C1 dev processes if the terminal was closed without Ctrl+C.

.DESCRIPTION
    1. Verifies that port 8001 is actively running the AssistLK Problem Understanding Agent before stopping it.
       Never terminates an unrecognized process on port 8001.
    2. Identifies any AssistLK.Api / dotnet process on port 5012 and safely stops it.
    3. Leaves all unrelated processes untouched.

.EXAMPLE
    .\scripts\stop-c1-dev.ps1
#>

[CmdletBinding()]
param(
    [int]$PythonPort = 8001,
    [int]$ApiPort = 5012
)

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " AssistLK Component 1 Development Process Stopper" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Stop Python Agent if it matches the AssistLK service
$healthUrl = "http://127.0.0.1:${PythonPort}/health"
try {
    $res = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 2 -ErrorAction Stop
    if ($res -and $res.status -eq "healthy" -and $res.service -eq "problem-understanding-agent") {
        $conn = Get-NetTCPConnection -LocalPort $PythonPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn -and $conn.OwningProcess) {
            Write-Host "Found AssistLK Python Agent on port ${PythonPort} (PID: $($conn.OwningProcess)). Stopping..." -ForegroundColor Yellow
            & taskkill /pid $conn.OwningProcess /t /f 2>$null | Out-Null
            Write-Host "Python Agent stopped." -ForegroundColor Green
        }
    } else {
        Write-Host "Port ${PythonPort} is in use, but does not identify as AssistLK Problem Understanding Agent. Leaving untouched." -ForegroundColor DarkGray
    }
} catch {
    # Not running or connection refused
    Write-Host "No active AssistLK Python Agent detected on port ${PythonPort}." -ForegroundColor DarkGray
}

# 2. Stop ASP.NET Core API on port 5012
try {
    $apiConn = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($apiConn -and $apiConn.OwningProcess) {
        $proc = Get-Process -Id $apiConn.OwningProcess -ErrorAction SilentlyContinue
        if ($proc -and ($proc.ProcessName -match "AssistLK\.Api|dotnet")) {
            Write-Host "Found AssistLK API on port ${ApiPort} (PID: $($apiConn.OwningProcess), Name: $($proc.ProcessName)). Stopping..." -ForegroundColor Yellow
            & taskkill /pid $apiConn.OwningProcess /t /f 2>$null | Out-Null
            Write-Host "ASP.NET Core API stopped." -ForegroundColor Green
        }
    }
} catch {
    Write-Host "No active AssistLK API detected on port ${ApiPort}." -ForegroundColor DarkGray
}

Write-Host "Stop operation complete.`n" -ForegroundColor Green
