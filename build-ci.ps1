param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$arguments = @('-Configuration', 'Release')
if ($Clean) { $arguments += '-Clean' }
& (Join-Path $PSScriptRoot 'build.ps1') @arguments
if ($LASTEXITCODE -ne 0) { throw "CI build failed with exit code $LASTEXITCODE" }
