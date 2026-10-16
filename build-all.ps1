# Builds self-contained releases for every platform into dist\ (one folder + one zip each).
# Usage: ./build-all.ps1 [-Version 0.1.0] [-Rids win-x64,linux-x64]
# Note: macOS .app bundles are best built with build-all.sh on a Mac (needs codesign for Apple Silicon).
param([string]$Version = "0.1.0", [string[]]$Rids = @("win-x64", "linux-x64", "osx-x64", "osx-arm64"))
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if (Test-Path dist) { Remove-Item dist -Recurse -Force }
New-Item -ItemType Directory dist | Out-Null
foreach ($rid in $Rids) {
    Write-Host "== $rid"
    $out = Join-Path dist "Sandbox-$rid"
    dotnet publish Sandbox.csproj -c Release -r $rid --self-contained true "-p:Version=$Version" -p:DebugType=none -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }
    if ($rid -like "osx-*") {
        $tmp = Join-Path dist ".app-$rid"
        Move-Item $out $tmp
        $macos = Join-Path $out "Sandbox.app/Contents/MacOS"
        New-Item -ItemType Directory -Force $macos, (Join-Path $out "Sandbox.app/Contents/Resources") | Out-Null
        Copy-Item (Join-Path $tmp "*") $macos -Recurse
        Remove-Item $tmp -Recurse -Force
        (Get-Content packaging/macos/Info.plist -Raw).Replace("__VERSION__", $Version) |
            Set-Content (Join-Path $out "Sandbox.app/Contents/Info.plist") -NoNewline
    }
    Compress-Archive -Path $out -DestinationPath (Join-Path dist "Sandbox-$Version-$rid.zip") -Force
}
Get-ChildItem dist
