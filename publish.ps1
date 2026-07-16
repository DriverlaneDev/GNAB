$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$output = Join-Path $root "Gnab\bin\Publish"

dotnet publish (Join-Path $root "Gnab\Gnab.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

Remove-Item (Join-Path $output "Gnab.pdb") -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Published: $output\Gnab.exe" -ForegroundColor Green
