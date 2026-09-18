param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
if ($Clean) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -Clean
} else {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}
if ($LASTEXITCODE -ne 0) { throw "CI build failed with exit code $LASTEXITCODE" }
