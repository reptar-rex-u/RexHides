$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "dist\personal"

Write-Host "Publishing personal RexHides build (G Home enabled)..."
dotnet publish (Join-Path $Root "src\RexHides\RexHides.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:EnableGHome=true `
    -o $OutDir

Write-Host "Published to $OutDir"
