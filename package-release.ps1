param(
    [string]$Version = 'v0.1.0-preview.1'
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
& (Join-Path $projectDir 'build.ps1') -Clean
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

$stage = Join-Path $projectDir 'release-stage'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $projectDir 'artifacts\publish\EnergyControl.exe') $stage
Copy-Item (Join-Path $projectDir 'README.md'), (Join-Path $projectDir 'README.zh-CN.md'),
    (Join-Path $projectDir 'LICENSE'),
    (Join-Path $projectDir 'CHANGELOG.md'), (Join-Path $projectDir 'DISCLAIMER.md') $stage
Copy-Item -LiteralPath (Join-Path $projectDir 'docs') -Destination $stage -Recurse

$zip = Join-Path $projectDir ("EnergyControl-for-Lenovo-$Version-windows-x64.zip")
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$hashPath = "$zip.sha256"
"$((Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant())  $(Split-Path $zip -Leaf)" |
    Set-Content -LiteralPath $hashPath -Encoding ascii
Write-Host "Package: $zip"
Write-Host "SHA-256: $hashPath"
