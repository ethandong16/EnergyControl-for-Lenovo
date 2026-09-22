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
& (Join-Path $projectDir 'docs\build-readme.ps1') -OutputPath (Join-Path $publishDir 'README.html')
Copy-Item -Path (Join-Path $projectDir '*.md') -Destination $publishDir -Force
Copy-Item -LiteralPath (Join-Path $projectDir 'LICENSE') -Destination $publishDir -Force
Copy-Item -LiteralPath (Join-Path $projectDir 'docs') -Destination $publishDir -Recurse -Force

Write-Host "Built: $publishedExe"
Write-Host 'Portable payload: EnergyControl.exe and multilingual help. Lenovo components are discovered at runtime.'
