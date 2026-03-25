$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$dataDir = Join-Path $scriptDir '.data'
if (Test-Path (Join-Path $dataDir 'backend.pid')) {
    [int]$backendPid = Get-Content (Join-Path $dataDir 'backend.pid')
    Write-Host "Stopping backend PID $backendPid (attempting taskkill)"
    try {
        & taskkill /PID $backendPid /T /F 2>$null
    } catch {
        Write-Host "taskkill failed, falling back to Stop-Process"
        Stop-Process -Id $backendPid -Force -ErrorAction SilentlyContinue
    }
    Remove-Item (Join-Path $dataDir 'backend.pid') -ErrorAction SilentlyContinue
}
if (Test-Path (Join-Path $dataDir 'frontend.pid')) {
    [int]$frontendPid = Get-Content (Join-Path $dataDir 'frontend.pid')
    Write-Host "Stopping frontend PID $frontendPid (attempting taskkill)"
    try {
        & taskkill /PID $frontendPid /T /F 2>$null
    } catch {
        Write-Host "taskkill failed, falling back to Stop-Process"
        Stop-Process -Id $frontendPid -Force -ErrorAction SilentlyContinue
    }
    Remove-Item (Join-Path $dataDir 'frontend.pid') -ErrorAction SilentlyContinue
}
if (Test-Path (Join-Path $dataDir 'test.db')) {
    Write-Host "Removing test DB"
    Remove-Item (Join-Path $dataDir 'test.db') -ErrorAction SilentlyContinue
}
Write-Host "Cleanup complete."
