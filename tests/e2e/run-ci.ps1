$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "Run CI: start services, run headless tests, stop services"

# Ensure Playwright dependencies are installed (skip if already installed)
if (-not (Test-Path (Join-Path $scriptDir 'node_modules')) ) {
    Write-Host "Installing npm dependencies..."
    & npm ci --prefix $scriptDir
}

# Ensure Playwright browsers are installed (needed for CI runners)
Write-Host "Installing Playwright browsers..."
Push-Location $scriptDir
try {
    & npx playwright install --with-deps
} finally {
    Pop-Location
}

$env:HEADLESS = 'true'

Write-Host "Starting services..."
& "$scriptDir\start-all.ps1"

Write-Host "Waiting for services to start..."
Start-Sleep -Seconds 6

Write-Host "Running headless tests in this shell..."
Push-Location $scriptDir
try {
    & npm run test
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

Write-Host "Tests finished with exit code $exitCode. Stopping services..."
& "$scriptDir\stop-all.ps1"

if ($exitCode -ne 0) { exit $exitCode } else { exit 0 }
