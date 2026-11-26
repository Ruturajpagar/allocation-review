#!/usr/bin/env bash
# Production build: Angular (optimised) + ASP.NET Core (Release), packaged as one app in artifacts/app.
# The API serves the Angular files from wwwroot, so the UI and API share one origin and the
# session cookie stays SameSite=Strict.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/app"

echo "==> Building the Angular app (production)"
(cd "$ROOT/web" && npm ci --no-audit --no-fund && npx ng build --configuration production)

echo "==> Publishing the API (Release)"
rm -rf "$OUT"
dotnet publish "$ROOT/api/src/AllocationReview.Api/AllocationReview.Api.csproj" -c Release -o "$OUT"

echo "==> Copying the Angular build into wwwroot"
mkdir -p "$OUT/wwwroot"
cp -R "$ROOT/web/dist/web/browser/." "$OUT/wwwroot/"

echo "Done. Run it with:"
echo "  ConnectionStrings__Default='Host=...;Database=...;Username=...;Password=...' dotnet $OUT/AllocationReview.Api.dll"
