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

It logs in using provided credentials or env vars and pushes both images.
#>

Param(
  [string]$Tag = "latest",
  [string[]]$Images = @("caaz/daily-challenge-web-backend", "caaz/daily-challenge-web-frontend"),
  [string]$Username = $env:DOCKERHUB_USERNAME,
  [string]$Password = $env:DOCKERHUB_TOKEN
)

function Fail($msg){ Write-Error $msg; exit 1 }

# If credentials are provided, perform docker login. Otherwise assume user already ran `docker login` locally.
$didLogin = $false
if ($Username -and $Password) {
  Write-Host "Logging into Docker Hub as $Username"
  docker login -u $Username -p $Password | Out-Host
  if ($LASTEXITCODE -ne 0) { Fail "docker login failed" }
  $didLogin = $true
}

foreach ($img in $Images) {
    $full = "$($img):$Tag"
    $imageId = docker images -q $full
    $exists = -not [string]::IsNullOrEmpty($imageId)
    if (-not $exists) {
      Write-Warning "Local image '$full' not found. Ensure you built it (docker compose build) or tagged it locally." 
    }

    Write-Host "Pushing $full"
    docker push $full | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "Failed to push $full" }
  }

  if ($didLogin) {
    Write-Host "Logging out"
    docker logout | Out-Host
  }

  Write-Host "All done."
