param(
    [switch]$Clean,
    [string]$AddinPath
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $projectDir 'bin'
$commonData = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$addinRoots = @(
    (Join-Path $commonData 'Lenovo\Vantage\Addins\IdeaNotebookAddin'),
    (Join-Path $commonData 'Lenovo\Commercial Vantage\Addins\IdeaNotebookAddin'),
    (Join-Path $commonData 'Lenovo\Lenovo Vantage\Addins\IdeaNotebookAddin')
)
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if ($Clean -and (Test-Path -LiteralPath $outputDir)) {
    Remove-Item -LiteralPath $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$addinDir = $null
if (-not [String]::IsNullOrWhiteSpace($AddinPath)) {
    if (-not (Test-Path -LiteralPath $AddinPath)) {
        throw "AddinPath not found: $AddinPath"
    }
    $hint = Get-Item -LiteralPath $AddinPath
    if ($hint.PSIsContainer) {
        $addinDir = $hint
    } else {
        $addinDir = $hint.Directory
    }
    if (-not (Test-Path -LiteralPath (Join-Path $addinDir.FullName 'IdeaNotebookAddin.dll'))) {
        throw "IdeaNotebookAddin.dll not found in AddinPath: $($addinDir.FullName)"
    }
} else {
    $candidates = @(
        foreach ($root in $addinRoots) {
            if (Test-Path -LiteralPath $root) {
                Get-ChildItem -LiteralPath $root -Directory |
                    Where-Object { $_.Name -match '^\d+(\.\d+)+$' }
            }
        }
    )
    $addinDir = $candidates |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1
}

if (-not $addinDir) { throw "IdeaNotebookAddin was not found under $addinRoot" }
if (-not (Test-Path -LiteralPath $compiler)) { throw "Compiler not found: $compiler" }

$references = @(
    'IdeaNotebookAddin.dll',
    'BatteryManagementContract.dll',
    'PowerContract.dll',
    'Lenovo.VantageService.Utilities.dll',
    'Newtonsoft.Json.dll'
)
foreach ($name in $references) {
    $source = Join-Path $addinDir.FullName $name
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Dependency not found: $source"
    }
}

# Keep the Addin's managed and native dependencies beside the executable.
Get-ChildItem -LiteralPath $addinDir.FullName -File -Filter '*.dll' |
    Copy-Item -Destination $outputDir -Force

# The percentage threshold API ships with selected Vantage add-ins rather than
# IdeaNotebookAddin. Copy its managed wrapper and x64 native client when present;
# runtime discovery remains available for installations with a different layout.
$powerRpcRoots = @(
    (Join-Path $commonData 'Lenovo\Vantage\Addins\DeviceSettingsHeartbeatAddin'),
    (Join-Path $commonData 'Lenovo\Vantage\Addins\MultimediaAddin'),
    (Join-Path $commonData 'Lenovo\Commercial Vantage\Addins\DeviceSettingsHeartbeatAddin'),
    (Join-Path $commonData 'Lenovo\Lenovo Vantage\Addins\DeviceSettingsHeartbeatAddin')
)
$powerRpcDir = @(
    foreach ($root in $powerRpcRoots) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Directory |
                Where-Object {
                    $_.Name -match '^\d+(\.\d+)+$' -and
                    (Test-Path -LiteralPath (Join-Path $_.FullName 'Lenovo.Vantage.PowerRpcClient.dll'))
                }
        }
    }
) | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if ($powerRpcDir) {
    Copy-Item -LiteralPath (
        Join-Path $powerRpcDir.FullName 'Lenovo.Vantage.PowerRpcClient.dll') `
        -Destination $outputDir -Force
    $nativePowerRpc = Join-Path $powerRpcDir.FullName 'x64\PowerRpcClient.dll'
    if (-not (Test-Path -LiteralPath $nativePowerRpc)) {
        $nativePowerRpc = Join-Path $powerRpcDir.FullName 'PowerRpcClient.dll'
    }
    if (Test-Path -LiteralPath $nativePowerRpc) {
        $nativeOutputDir = Join-Path $outputDir 'x64'
        New-Item -ItemType Directory -Path $nativeOutputDir -Force | Out-Null
        Copy-Item -LiteralPath $nativePowerRpc -Destination $nativeOutputDir -Force
    }
} else {
    Write-Warning 'Lenovo Power RPC client was not found; custom charge thresholds will be unavailable.'
}

$compilerArgs = @(
    '/nologo', '/target:exe', '/platform:x64', '/optimize+',
    ('/out:' + (Join-Path $outputDir 'LenovoSettingsDemo.exe')),
    ('/reference:' + (Join-Path $addinDir.FullName 'BatteryManagementContract.dll')),
    ('/reference:' + (Join-Path $addinDir.FullName 'PowerContract.dll')),
    ('/reference:' + (Join-Path $addinDir.FullName 'Lenovo.VantageService.Utilities.dll')),
    ('/reference:' + (Join-Path $addinDir.FullName 'Newtonsoft.Json.dll')),
    '/reference:System.Management.dll',
    (Join-Path $projectDir 'Compatibility.cs'),
    (Join-Path $projectDir 'DirectChargeMode.cs'),
    (Join-Path $projectDir 'ChargeThreshold.cs'),
    (Join-Path $projectDir 'Program.cs')
)

& $compiler $compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "CLI compilation failed with exit code $LASTEXITCODE"
}

$guiCompilerArgs = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    ('/win32manifest:' + (Join-Path $projectDir 'app.manifest')),
    ('/out:' + (Join-Path $outputDir 'LenovoSettingsGui.exe')),
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Management.dll',
    ('/reference:' + (Join-Path $addinDir.FullName 'BatteryManagementContract.dll')),
    ('/reference:' + (Join-Path $addinDir.FullName 'PowerContract.dll')),
    ('/reference:' + (Join-Path $addinDir.FullName 'Lenovo.VantageService.Utilities.dll')),
    ('/reference:' + (Join-Path $addinDir.FullName 'Newtonsoft.Json.dll')),
    (Join-Path $projectDir 'Compatibility.cs'),
    (Join-Path $projectDir 'DirectChargeMode.cs'),
    (Join-Path $projectDir 'ChargeThreshold.cs'),
    (Join-Path $projectDir 'Gui.cs')
)

& $compiler $guiCompilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "GUI compilation failed with exit code $LASTEXITCODE"
}

$addinConfig = Join-Path $addinDir.FullName 'IdeaNotebookAddin.dll.config'
if (Test-Path -LiteralPath $addinConfig) {
    foreach ($exeName in @('LenovoSettingsDemo.exe', 'LenovoSettingsGui.exe')) {
        Copy-Item -LiteralPath $addinConfig -Destination (
            Join-Path $outputDir ($exeName + '.config')) -Force
    }
}

Write-Host "Built: $(Join-Path $outputDir 'LenovoSettingsDemo.exe')"
Write-Host "Built: $(Join-Path $outputDir 'LenovoSettingsGui.exe')"
Write-Host "IdeaNotebookAddin version: $($addinDir.Name)"
