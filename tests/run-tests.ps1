param(
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($Executable)) {
    $Executable = Join-Path $projectDir 'bin\Release\net48\EnergyControl.exe'
}
if (-not (Test-Path -LiteralPath $Executable)) { throw "Executable not found: $Executable" }

$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $Executable).Path)
$flags = [Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
$protocolType = $asm.GetType('LenovoSettingsCompat.EnergyDriverProtocol', $true)
$clientType = $asm.GetType('LenovoSettingsCompat.EnergyDriverChargeClient', $true)
$modeType = $asm.GetType('LenovoSettingsCompat.DirectChargeMode', $true)
$responseType = $asm.GetType('LenovoSettingsCompat.AddinResponse', $true)
$thresholdType = $asm.GetType('LenovoSettingsCompat.ChargeThresholdClient', $true)

function Get-Mode([string]$name) { [Enum]::Parse($modeType, $name) }
function Get-Commands($state, [string]$name) {
    $method = $clientType.GetMethod('PlanCommands', $flags)
    return @($method.Invoke($null, [object[]]@($state, (Get-Mode $name))) | ForEach-Object { [int]$_ })
}

$decode = $clientType.GetMethod('DecodeFlags', $flags)
$state = $decode.Invoke($null, [object[]]([uint32]0x00860022))
if (-not $state.StorageEnabled -or $state.Storage80Capable -or -not $state.QuickCapable -or $state.QuickEnabled) {
    throw '0x00860022 flag decoding failed'
}

$storage = $decode.Invoke($null, [object[]]([uint32]0x00024020))
$quick = $decode.Invoke($null, [object[]]([uint32]0x00020004))
$normal = $decode.Invoke($null, [object[]]([uint32]0x00024000))
if ((Get-Commands $normal 'Storage') -join ',' -ne '3,13') { throw 'Normal -> storage command sequence failed' }
if ((Get-Commands $storage 'Normal') -join ',' -ne '5,15') { throw 'Storage -> normal command sequence failed' }
if ((Get-Commands $storage 'Quick') -join ',' -ne '7,5,15') { throw 'Storage -> quick command sequence failed' }
if ((Get-Commands $quick 'Normal') -join ',' -ne '8') { throw 'Quick -> normal command sequence failed' }

$badJson = $responseType.GetMethod('ToDictionary', $flags).Invoke($null, [object[]]@('not-json'))
if ($badJson.Count -ne 0) { throw 'Malformed JSON did not degrade safely' }
$validate = $thresholdType.GetMethod('ValidateValues', $flags)
$validate.Invoke($null, [object[]]@(75, 80)) | Out-Null
$rejected = $false
try { $validate.Invoke($null, [object[]]@(80, 75)) | Out-Null } catch { $rejected = $true }
if (-not $rejected) { throw 'Invalid threshold order was accepted' }

$references = $asm.GetReferencedAssemblies() | ForEach-Object { $_.Name }
foreach ($forbidden in @('Newtonsoft.Json', 'BatteryManagementContract', 'PowerContract', 'IdeaNotebookAddin', 'Lenovo.Vantage.PowerRpcClient')) {
    if ($references -contains $forbidden) { throw "Forbidden assembly reference found: $forbidden" }
}

$previousAddin = $env:LENOVO_SETTINGS_ADDIN_PATH
$previousRpc = $env:LENOVO_POWER_RPC_PATH
try {
    $env:LENOVO_SETTINGS_ADDIN_PATH = Join-Path $projectDir '.ci-missing-addin'
    $env:LENOVO_POWER_RPC_PATH = Join-Path $projectDir '.ci-missing-rpc'
    & $Executable charge direct set normal
    if ($LASTEXITCODE -ne 1) { throw "Missing --apply check returned $LASTEXITCODE" }
    & $Executable help | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'CLI help smoke test failed' }
} finally {
    $env:LENOVO_SETTINGS_ADDIN_PATH = $previousAddin
    $env:LENOVO_POWER_RPC_PATH = $previousRpc
}

Write-Host 'Protocol, safety, JSON, and dependency tests: PASS'
