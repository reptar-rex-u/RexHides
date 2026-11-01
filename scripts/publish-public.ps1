$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "dist\public"
$Project = Join-Path $Root "src\RexHides\RexHides.csproj"

Write-Host "Publishing public RexHides build (self-contained)..."
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutDir

$Version = (Select-Xml -Path $Project -XPath "//Version").Node.InnerText
$BuiltAt = Get-Date -Format "yyyy-MM-dd HH:mm"
$ZipName = "RexHides-$Version-win-x64.zip"
$ZipPath = Join-Path $Root "dist\$ZipName"

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path (Join-Path $OutDir "RexHides.exe") -DestinationPath $ZipPath -Force

$IndexHtml = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>RexHides Downloads</title>
  <style>
    :root { color-scheme: dark; }
    body { font-family: Segoe UI, system-ui, sans-serif; background: #121218; color: #ececf1; margin: 0; }
    main { max-width: 720px; margin: 0 auto; padding: 48px 24px; }
    h1 { margin: 0 0 8px; font-size: 2rem; }
    p { color: #b4b4c7; line-height: 1.5; }
    .card { background: #1f1f2e; border: 1px solid #34344a; border-radius: 12px; padding: 20px; margin-top: 24px; }
    a.button { display: inline-block; margin-top: 12px; background: #e5a00d; color: #121218; text-decoration: none; font-weight: 600; padding: 12px 18px; border-radius: 8px; }
    code { background: #2a2a3d; padding: 2px 6px; border-radius: 4px; }
    ul { color: #d8d8e8; }
  </style>
</head>
<body>
  <main>
    <h1>RexHides</h1>
    <p>Minimize any Windows application window to its own system tray icon.</p>
    <div class="card">
      <h2>Download</h2>
      <p>Version <strong>$Version</strong> · Built $BuiltAt · Windows 10/11 x64 · No separate .NET install required</p>
      <a class="button" href="RexHides.exe">Download RexHides.exe</a>
      <p>Run with <code>--minimized</code> to start in the tray only.</p>
    </div>
    <div class="card">
      <h2>Features</h2>
      <ul>
        <li>Global hotkey minimize-to-tray</li>
        <li>Per-window tray icons with restore</li>
        <li>Auto-hide rules</li>
        <li>Optional title-bar minimize button</li>
        <li>Crash recovery and watchdog restore</li>
      </ul>
    </div>
  </main>
</body>
</html>
"@

Set-Content -Path (Join-Path $OutDir "index.html") -Value $IndexHtml -Encoding UTF8
Write-Host "Published to $OutDir"
Write-Host "Release zip: $ZipPath"
