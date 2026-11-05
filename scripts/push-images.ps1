<#
Push images to Docker Hub.

Usage examples:
  # use env vars DOCKERHUB_USERNAME and DOCKERHUB_TOKEN
  .\scripts\push-images.ps1 -Tag "v1.0.0"

  # provide credentials explicitly
  .\scripts\push-images.ps1 -Tag latest -Username myuser -Password mytoken

This script expects the images to already exist locally with the names:
  caaz/daily-challenge-web-backend:Tag
  caaz/daily-challenge-web-frontend:Tag

It logs in using provided credentials or env vars (password passed via stdin,
never on the command line), pushes both images, and always logs out again if it
logged in, even when a push fails.
#>

Param(
  [string]$Tag = "latest",
  [string[]]$Images = @("caaz/daily-challenge-web-backend", "caaz/daily-challenge-web-frontend"),
  [string]$Username = $env:DOCKERHUB_USERNAME,
  [string]$Password = $env:DOCKERHUB_TOKEN
)

$didLogin = $false
try {
  # If credentials are provided, perform docker login. Otherwise assume user already ran `docker login` locally.
  if ($Username -and $Password) {
    Write-Host "Logging into Docker Hub as $Username"
    $Password | docker login --username $Username --password-stdin | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "docker login failed" }
    $didLogin = $true
  }

  foreach ($img in $Images) {
    $full = "$($img):$Tag"
    $imageId = docker images -q $full
    if ([string]::IsNullOrEmpty($imageId)) {
      Write-Warning "Local image '$full' not found. Ensure you built it (docker compose build) or tagged it locally."
    }

    Write-Host "Pushing $full"
    docker push $full | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Failed to push $full" }
  }

  Write-Host "All done."
}
catch {
  Write-Host "ERROR: $_" -ForegroundColor Red
  exit 1
}
finally {
  if ($didLogin) {
    Write-Host "Logging out"
    docker logout | Out-Host
  }
}
