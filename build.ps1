param(
    [switch]$Clean,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$project = Join-Path $projectDir 'EnergyControl.csproj'
$publishDir = Join-Path $projectDir 'artifacts\publish'

if ($Clean) {
    foreach ($path in @(
        (Join-Path $projectDir 'artifacts'),
        (Join-Path $projectDir 'bin'),
        (Join-Path $projectDir 'obj')
    )) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

$nugetConfig = Join-Path $projectDir 'NuGet.Config'
dotnet restore $project --configfile $nugetConfig --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

dotnet build $project -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
$builtExe = Join-Path $projectDir ("bin\$Configuration\net48\EnergyControl.exe")
if (-not (Test-Path -LiteralPath $builtExe)) { throw "Built executable not found: $builtExe" }
$publishedExe = Join-Path $publishDir 'EnergyControl.exe'
Copy-Item -LiteralPath $builtExe -Destination $publishedExe -Force

Write-Host "Built: $publishedExe"
Write-Host 'The portable runtime payload contains only EnergyControl.exe; Lenovo components are discovered from the installed system at runtime.'
