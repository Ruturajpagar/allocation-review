# Production build: Angular (optimised) + ASP.NET Core (Release), packaged as one app in artifacts\app.
# The API serves the Angular files from wwwroot, so the UI and API share one origin and the
# session cookie stays SameSite=Strict.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\app'

Write-Host '==> Building the Angular app (production)'
Push-Location (Join-Path $root 'web')
try {
    npm ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
    npx ng build --configuration production
    if ($LASTEXITCODE -ne 0) { throw 'ng build failed' }
}
finally { Pop-Location }

Write-Host '==> Publishing the API (Release)'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root 'api\src\AllocationReview.Api\AllocationReview.Api.csproj') -c Release -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host '==> Copying the Angular build into wwwroot'
$wwwroot = Join-Path $out 'wwwroot'
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item (Join-Path $root 'web\dist\web\browser\*') $wwwroot -Recurse -Force

Write-Host "Done. Run it with:"
Write-Host "  `$env:ConnectionStrings__Default = 'Host=...;Database=...;Username=...;Password=...'"
Write-Host "  dotnet $out\AllocationReview.Api.dll"
