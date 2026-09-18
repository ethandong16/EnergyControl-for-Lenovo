param(
    [switch]$Clean,
    [string]$NewtonsoftPath
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$outputDir = Join-Path $projectDir 'ci-bin'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if ($Clean -and (Test-Path -LiteralPath $outputDir)) {
    Remove-Item -LiteralPath $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "64-bit .NET Framework compiler not found: $compiler"
}

if ([String]::IsNullOrWhiteSpace($NewtonsoftPath)) {
    $candidates = @(
        Join-Path $projectDir 'packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll'
        Join-Path $projectDir 'bin\Newtonsoft.Json.dll'
    )
    $NewtonsoftPath = $candidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}
if ([String]::IsNullOrWhiteSpace($NewtonsoftPath) -or
    -not (Test-Path -LiteralPath $NewtonsoftPath)) {
    throw 'Newtonsoft.Json.dll was not found. Run NuGet restore or pass -NewtonsoftPath.'
}

function Invoke-Compiler {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    $logPath = Join-Path $outputDir ($Name + '.log')
    $output = & $compiler $Arguments 2>&1
    $output | Set-Content -LiteralPath $logPath -Encoding UTF8
    if ($LASTEXITCODE -ne 0) {
        $output | Write-Host
        throw "$Name compilation failed with exit code $LASTEXITCODE"
    }
}

$batteryContract = Join-Path $outputDir 'BatteryManagementContract.dll'
$powerContract = Join-Path $outputDir 'PowerContract.dll'

Invoke-Compiler 'BatteryManagementContract' @(
    '/nologo', '/target:library', '/platform:anycpu', '/optimize+',
    ('/out:' + $batteryContract),
    (Join-Path $projectDir 'ci\BatteryManagementContract.Stub.cs')
)

Invoke-Compiler 'PowerContract' @(
    '/nologo', '/target:library', '/platform:anycpu', '/optimize+',
    ('/out:' + $powerContract),
    (Join-Path $projectDir 'ci\PowerContract.Stub.cs')
)

$commonReferences = @(
    ('/reference:' + $batteryContract),
    ('/reference:' + $powerContract),
    ('/reference:' + $NewtonsoftPath),
    '/reference:System.Management.dll'
)

Invoke-Compiler 'LenovoSettingsDemo' (@(
    '/nologo', '/target:exe', '/platform:x64', '/optimize+',
    ('/out:' + (Join-Path $outputDir 'LenovoSettingsDemo.exe'))
) + $commonReferences + @(
    (Join-Path $projectDir 'Compatibility.cs'),
    (Join-Path $projectDir 'DirectChargeMode.cs'),
    (Join-Path $projectDir 'ChargeThreshold.cs'),
    (Join-Path $projectDir 'Program.cs')
))

Invoke-Compiler 'LenovoSettingsGui' (@(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    ('/win32manifest:' + (Join-Path $projectDir 'app.manifest')),
    ('/out:' + (Join-Path $outputDir 'LenovoSettingsGui.exe')),
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll'
) + $commonReferences + @(
    (Join-Path $projectDir 'Compatibility.cs'),
    (Join-Path $projectDir 'DirectChargeMode.cs'),
    (Join-Path $projectDir 'ChargeThreshold.cs'),
    (Join-Path $projectDir 'Gui.cs')
))

Copy-Item -LiteralPath $NewtonsoftPath -Destination $outputDir -Force
Write-Host "CI compilation succeeded: $outputDir"
Write-Host 'The generated Contract DLLs are compile-time stubs and must not be distributed.'
