Param(
    [switch]$Headed
)

$ErrorActionPreference = 'Stop'

if ($Headed) { $env:HEADLESS = 'false' } else { $env:HEADLESS = 'true' }

Write-Host "Running Playwright tests with video enabled (config: playwright.video.config.ts)"

# Ensure Playwright browsers are installed
npx playwright install --with-deps

# Run tests with the alternate config that enables video for all runs
npx playwright test --config=playwright.video.config.ts
