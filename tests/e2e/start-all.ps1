$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$dataDir = Join-Path $scriptDir '.data'
if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir | Out-Null }

Write-Host "Starting backend and frontend (PowerShell)..."

## Start backend (dotnet run) with isolated SQLite DB
$dbPath = Join-Path $dataDir 'test.db'

# Ensure fresh test DB so seeded admin is the first user
if (Test-Path $dbPath) {
	Write-Host "Removing existing test DB at $dbPath"
	Remove-Item $dbPath -Force -ErrorAction SilentlyContinue
}

$env:SQLITE_PATH = $dbPath
$env:ASPNETCORE_ENVIRONMENT = 'IntegrationTests'
$env:ASPNETCORE_URLS = 'http://localhost:5000'
Write-Host "-> Starting backend with SQLITE_PATH=$dbPath"
$backendOut = Join-Path $dataDir 'backend.out.log'
$backendErr = Join-Path $dataDir 'backend.err.log'
$backendProc = Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','..\..\backend' -WorkingDirectory $scriptDir -RedirectStandardOutput $backendOut -RedirectStandardError $backendErr -PassThru
Set-Content -Path (Join-Path $dataDir 'backend.pid') -Value $backendProc.Id
Write-Host "   Backend PID $($backendProc.Id) started; logs: $backendOut, $backendErr"

Start-Sleep -Seconds 2

## Start frontend (npm run dev)
$frontendDir = Join-Path $scriptDir '..\..\frontend'
Write-Host "-> Starting frontend dev server (npm run dev) in $frontendDir"
$frontendOut = Join-Path $dataDir 'frontend.out.log'
$frontendErr = Join-Path $dataDir 'frontend.err.log'
## On Windows `npm` may resolve to a PowerShell wrapper (`npm.ps1`).
## `Start-Process` cannot directly start that wrapper, so prefer `npm.cmd`
$npmCmdObj = Get-Command npm.cmd -ErrorAction SilentlyContinue
if ($npmCmdObj) {
	$npmCmd = $npmCmdObj.Source
} else {
	$npmCmd = $null
}

if ($npmCmd) {
	$startFile = $npmCmd
	$startArgs = @('run','dev')
} else {
	# Fallback to running via cmd.exe which will resolve the correct npm executable
	$startFile = 'cmd.exe'
	$startArgs = @('/c','npm','run','dev')
}

$frontendProc = Start-Process -FilePath $startFile -ArgumentList $startArgs -WorkingDirectory $frontendDir -RedirectStandardOutput $frontendOut -RedirectStandardError $frontendErr -PassThru
Set-Content -Path (Join-Path $dataDir 'frontend.pid') -Value $frontendProc.Id
Write-Host "   Frontend PID $($frontendProc.Id) started; logs: $frontendOut, $frontendErr"

Write-Host "Both services started. Use 'npm run stop:ps' to stop and cleanup."
