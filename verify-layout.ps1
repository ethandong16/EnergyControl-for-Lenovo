param(
    [switch]$LiveRead,
    [string]$Executable,
    [string]$ScreenshotDirectory,
    [ValidateSet('zh-CN', 'en-US', 'ja-JP')]
    [string]$Culture
)
$ErrorActionPreference = 'Stop'
if ([String]::IsNullOrWhiteSpace($Culture)) {
    foreach ($language in @('zh-CN','en-US','ja-JP')) {
        $parameters = @{ Culture=$language }
        if ($Executable) { $parameters.Executable=$Executable }
        if ($ScreenshotDirectory) { $parameters.ScreenshotDirectory=$ScreenshotDirectory }
        if ($LiveRead) { $parameters.LiveRead=$true }
        & (Get-Process -Id $PID).Path -NoProfile -File $PSCommandPath @parameters
        if ($LASTEXITCODE -ne 0) { throw "GUI verification failed for $language" }
    }
    return
}
[Globalization.CultureInfo]::CurrentUICulture = [Globalization.CultureInfo]::GetCultureInfo($Culture)
[Globalization.CultureInfo]::DefaultThreadCurrentUICulture = [Globalization.CultureInfo]::CurrentUICulture
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$projectDir = $PSScriptRoot
$exePath = $Executable
if ([String]::IsNullOrWhiteSpace($exePath)) {
    $exePath = Join-Path $projectDir 'bin\Release\net48\EnergyControl.exe'
}
if (-not (Test-Path -LiteralPath $exePath)) {
    $exePath = Join-Path $projectDir 'artifacts\publish\EnergyControl.exe'
}
$asm = [Reflection.Assembly]::LoadFrom($exePath)
if ([String]::IsNullOrWhiteSpace($ScreenshotDirectory)) {
    $ScreenshotDirectory = Join-Path $projectDir 'artifacts\layout'
}
$ScreenshotDirectory = Join-Path $ScreenshotDirectory $Culture
New-Item -ItemType Directory -Path $ScreenshotDirectory -Force | Out-Null
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$allFlags = [Reflection.BindingFlags]'Static,Instance,NonPublic,Public'
$formType = $asm.GetType('LenovoSettingsGui.MainForm', $true)
$stateType = $asm.GetType('LenovoSettingsGui.DeviceState', $true)
$state = [Activator]::CreateInstance($stateType, $true)
$textType = $asm.GetType('LenovoSettingsCompat.UiText', $true)
$translate = $textType.GetMethod('Get', $allFlags)
$sample = @{
    ChargeMode='Normal'; SupportedChargeModes='Normal,Storage,Quick'
    ChargeBackend='直接驱动'; ChargeLimitInfo='固件预设（未报告固定 80% 能力）'
    PerformanceMode='MMC_Performance'
    SupportedPerformanceModes='MMC_Auto,MMC_Cool,MMC_Performance,MMC_Geek'
        WorkingDriver='dispatcher'; ErrorCode='0'
        ShowGeekAsCreator='False'; ShowBsmAsQuietBsm='False'; IsGeekOptionGrey='False'
        ChargeWritable=$true; PerformanceWritable=$true
        ThresholdCapable=$true; ThresholdEnabled=$true; ThresholdWritable=$true
        ThresholdStart=75; ThresholdStop=80
        KeyboardBacklightSupported=$true; KeyboardBacklightWritable=$true
        KeyboardBacklightRestoreWritable=$true
        KeyboardBacklightReserveWritable=$true; KeyboardBacklightAutoDimWritable=$false
        KeyboardBacklightStatus='Level_2'; KeyboardBacklightLevelCapability='TwoLevelsAuto'
        KeyboardBacklightReserve='False'; KeyboardBacklightAutoDimCapability='False'
        KeyboardBacklightAutoDimStatus='NoCapability'; KeyboardBacklightAgent='IdeaNotebookAddin.dll 1.0.13.79'
}
$sample.ChargeBackend = $translate.Invoke($null,@($sample.ChargeBackend))
$sample.ChargeLimitInfo = $translate.Invoke($null,@($sample.ChargeLimitInfo))

# An unavailable percentage API must collapse its editor instead of leaving
# misleading default 75/80 values visible.
$unavailableForm = [Activator]::CreateInstance($formType,$true)
try {
    $unavailableState = [Activator]::CreateInstance($stateType,$true)
    foreach ($key in $sample.Keys) {
        $stateType.GetField($key,$flags).SetValue($unavailableState,$sample[$key])
    }
    $stateType.GetField('ThresholdCapable',$flags).SetValue($unavailableState,$false)
    $stateType.GetField('ThresholdWritable',$flags).SetValue($unavailableState,$false)
    $stateType.GetField('ThresholdError',$flags).SetValue(
        $unavailableState,'初始化充电阈值接口失败：RPC 服务未运行')
    $formType.GetMethod('DisplayState',$flags).Invoke(
        $unavailableForm,@($unavailableState)) | Out-Null
    $unavailableThreshold = $formType.GetField(
        'thresholdControls',$flags).GetValue($unavailableForm)
    if ($unavailableThreshold.Visible) {
        throw 'Unavailable threshold editor must be collapsed'
    }
} finally { $unavailableForm.Dispose() }
foreach ($key in $sample.Keys) { $stateType.GetField($key,$flags).SetValue($state,$sample[$key]) }
$capabilityType = $asm.GetType('LenovoSettingsCompat.CapabilityNames', $true)
$matchesMethod = $capabilityType.GetMethod('Matches', $allFlags)
$splitMethod = $capabilityType.GetMethod('Split', $allFlags)
$responseType = $asm.GetType('LenovoSettingsCompat.AddinResponse', $true)
$toDictionaryMethod = $responseType.GetMethod('ToDictionary', $allFlags)
$thresholdClientType = $asm.GetType('LenovoSettingsCompat.ChargeThresholdClient', $true)
$validateThresholdMethod = $thresholdClientType.GetMethod('ValidateValues', $allFlags)
if (-not $matchesMethod.Invoke($null, [object[]]@('ITS_Auto;Balanced|MMC_Cool', [string[]]@('Auto','ITS_Auto')))) {
    throw 'Capability aliases/separators are not recognized'
}
if (@($splitMethod.Invoke($null, [object[]]@('Normal;Storage|Quick'))).Count -ne 3) {
    throw 'Capability separator parsing failed'
}
if ($toDictionaryMethod.Invoke($null, [object[]]@('not-json')).Count -ne 0) {
    throw 'Malformed Addin responses must degrade to an empty object'
}
$validateThresholdMethod.Invoke($null, [object[]]@(75,80)) | Out-Null
$invalidThresholdRejected = $false
try {
    $validateThresholdMethod.Invoke($null, [object[]]@(80,75)) | Out-Null
} catch {
    $invalidThresholdRejected = $true
}
if (-not $invalidThresholdRejected) {
    throw 'Invalid charge threshold order was accepted'
}
if ($LiveRead) {
    $cli = $exePath
    $charge = (& $cli charge get | Out-String | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw 'Charge getter failed' }
    $power = (& $cli performance get | Out-String | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw 'Performance getter failed' }
    $settings = @{}
    foreach ($item in @($charge.settingList) + @($power.settingList)) {
        $settings[$item.key] = $item.value
    }
    $mapping = @{
        ChargeMode='BatteryChargeMode'; SupportedChargeModes='Supported-BatteryChargeMode'
        PerformanceMode='ITSMode'; SupportedPerformanceModes='Supported-ITSMode'
        WorkingDriver='WorkingDriver'; ShowGeekAsCreator='ShowGeekAsCreator'
        ErrorCode='ErrorCode'
    }
    foreach ($key in $mapping.Keys) {
        $stateType.GetField($key,$flags).SetValue($state,$settings[$mapping[$key]])
    }
}
function LayoutTree([System.Windows.Forms.Control]$control) {
    $control.PerformLayout()
    foreach ($child in $control.Controls) { LayoutTree $child }
    $control.PerformLayout()
}
function GetFontSnapshot([System.Windows.Forms.Control]$control) {
    [PSCustomObject]@{ Control=$control; Font=$control.Font }
    foreach ($child in $control.Controls) { GetFontSnapshot $child }
}
function CheckTree([System.Windows.Forms.Control]$control) {
    foreach ($child in $control.Controls) {
        if (-not $child.Visible) { continue }
        if (-not $control.ClientRectangle.Contains($child.Bounds) -and
            -not $control.AutoScroll) {
            throw ("Clipped control: '{0}', bounds {1}, parent {2}" -f $child.Text,$child.Bounds,$control.ClientRectangle)
        }
        if ($child -is [System.Windows.Forms.Label] -and $child.Text) {
            $preferred = $child.GetPreferredSize([Drawing.Size]::new($child.Width,0))
            if ($preferred.Height -gt $child.Height) {
                throw ("Clipped label: " + $child.Text)
            }
        }
        CheckTree $child
    }
    $children = @($control.Controls | Where-Object Visible)
    for ($i=0; $i -lt $children.Count; $i++) {
        for ($j=$i+1; $j -lt $children.Count; $j++) {
            if ($children[$i].Bounds.IntersectsWith($children[$j].Bounds)) {
                throw ("Overlap in {0} {1}: {2} '{3}' {4}; {5} '{6}' {7}" -f
                    $control.GetType().Name,$control.ClientSize,
                    $children[$i].GetType().Name,$children[$i].Text,$children[$i].Bounds,
                    $children[$j].GetType().Name,$children[$j].Text,$children[$j].Bounds)
            }
        }
    }
}
$scenarios = @(
    @{Name='default'; Width=640; Height=540; Scale=1.0}
    @{Name='narrow'; Width=560; Height=540; Scale=1.0}
    @{Name='wide'; Width=860; Height=540; Scale=1.0}
    @{Name='150-percent'; Width=640; Height=540; Scale=1.5}
    @{Name='200-percent'; Width=560; Height=540; Scale=2.0}
)
foreach ($scenario in $scenarios) {
    $form = [Activator]::CreateInstance($formType,$true)
    try {
        # Remove the hardware callback before showing the form for rendering.
        $formType.GetMethod('DisableInitialRefresh',$flags).Invoke($form,$null) | Out-Null
        $eventList = [System.ComponentModel.Component].GetProperty('Events',$flags).GetValue($form,$null)
        $shownField = [System.Windows.Forms.Form].GetField('s_shownEvent',[Reflection.BindingFlags]'NonPublic,Static')
        if ($shownField) {
            $shownKey = $shownField.GetValue($null)
            if ($eventList[$shownKey]) { $eventList.RemoveHandler($shownKey,$eventList[$shownKey]) }
        }
        $form.ShowInTaskbar = $false
        $form.Opacity = 0
        $formType.GetMethod('DisplayState',$flags).Invoke($form,@($state)) | Out-Null
        $formType.GetMethod('SetBusy',$flags).Invoke($form,@($false,$null)) | Out-Null
        $formType.GetMethod('SetStatus',$flags).Invoke($form,@($translate.Invoke($null,@('读取成功')),[Drawing.Color]::SeaGreen)) | Out-Null
        $form.ClientSize = [Drawing.Size]::new($scenario.Width,$scenario.Height)
        if ($scenario.Scale -ne 1.0) {
            $fontSnapshot = @(GetFontSnapshot $form)
            $form.Scale([Drawing.SizeF]::new($scenario.Scale,$scenario.Scale))
            foreach ($entry in $fontSnapshot) {
                $entry.Control.Font = [Drawing.Font]::new(
                    $entry.Font.FontFamily,
                    [single]($entry.Font.Size * $scenario.Scale),
                    $entry.Font.Style)
            }
        }
        LayoutTree $form
        $windowHandle = $form.Handle
        $form.Show()
        [System.Windows.Forms.Application]::DoEvents()
        LayoutTree $form
        $bitmap = [Drawing.Bitmap]::new($form.Width,$form.Height)
        try {
            $tabs = $formType.GetField('settingsTabs',$flags).GetValue($form)
            if ($tabs.TabPages.Count -ne 4) { throw 'Expected battery, performance, keyboard and diagnostics tabs' }
            $expectedTabs = switch ($Culture) {
                'zh-CN' { @('电池','性能','键盘','诊断') }
                'ja-JP' { @('バッテリー','パフォーマンス','キーボード','診断') }
                default { @('Battery','Performance','Keyboard','Diagnostics') }
            }
            for ($index=0; $index -lt 4; $index++) {
                if ($tabs.TabPages[$index].Text -ne $expectedTabs[$index]) {
                    throw "Incorrect tab translation for $Culture at $index"
                }
            }
            for ($tabIndex = 0; $tabIndex -lt $tabs.TabPages.Count; $tabIndex++) {
                $tabs.SelectedIndex = $tabIndex
                [System.Windows.Forms.Application]::DoEvents()
                LayoutTree $form
                $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
                CheckTree $form
                $bitmap.Save((Join-Path $ScreenshotDirectory ('layout-'+$scenario.Name+'-'+$tabIndex+'.png')),[Drawing.Imaging.ImageFormat]::Png)
            }
            $tabs.SelectedIndex = 2
            $formType.GetMethod('SetBusy',$flags).Invoke($form,@($true,$null)) | Out-Null
            $formType.GetMethod('DisplayState',$flags).Invoke($form,@($state)) | Out-Null
            $reserve = $formType.GetField('keyboardBacklightReserveButton',$flags).GetValue($form)
            $autoDim = $formType.GetField('keyboardBacklightAutoDimButton',$flags).GetValue($form)
            $restore = $formType.GetField('keyboardBacklightDefaultButton',$flags).GetValue($form)
            if ($reserve.Enabled -or $autoDim.Enabled -or $restore.Enabled) {
                throw 'Keyboard actions must be disabled during refresh'
            }
            $formType.GetMethod('SetBusy',$flags).Invoke($form,@($false,$null)) | Out-Null
            if (-not $reserve.Enabled -or -not $restore.Enabled -or $autoDim.Enabled) {
                throw 'Refresh must restore supported actions and keep unsupported auto-dim disabled'
            }
            $booleanCheck = $formType.GetMethod('IsBooleanValue',$allFlags)
            if ($booleanCheck.Invoke($null,@('NoCapability',$false))) {
                throw 'Unknown boolean state must not be treated as Off'
            }
            $chargePanel = $formType.GetField('chargeModes',$flags).GetValue($form)
            $thresholdPanel = $formType.GetField('thresholdControls',$flags).GetValue($form)
            $performancePanel = $formType.GetField('performanceModes',$flags).GetValue($form)
            $backlightPanel = $formType.GetField('keyboardBacklightModes',$flags).GetValue($form)
            $backlightActions = $formType.GetField('keyboardBacklightActions',$flags).GetValue($form)
            if ($chargePanel.Controls.Count -ne 3) {
                throw ("Expected 3 charging modes, found " + $chargePanel.Controls.Count)
            }
            if ($performancePanel.Controls.Count -ne 4) {
                throw ("Expected 4 supported performance modes, found " + $performancePanel.Controls.Count)
            }
            if ($backlightPanel.Controls.Count -ne 4) {
                throw ("Expected 4 supported keyboard backlight modes, found " + $backlightPanel.Controls.Count)
            }
            if ($backlightActions.Controls.Count -ne 3) {
                throw ("Expected 3 keyboard backlight actions, found " + $backlightActions.Controls.Count)
            }
            if (-not $thresholdPanel.Enabled -or $thresholdPanel.Controls.Count -ne 6) {
                throw 'Charge threshold controls are unavailable or incomplete'
            }
            foreach ($panel in @($chargePanel,$performancePanel,$backlightPanel)) {
                if ($panel.AutoScroll) { throw 'Mode panel must not scroll' }
                foreach ($button in $panel.Controls) {
                    if (-not $panel.ClientRectangle.Contains($button.Bounds)) {
                        throw ("Mode button clipped: {0}, bounds {1}, panel {2}" -f
                            $button.Text,$button.Bounds,$panel.ClientRectangle)
                    }
                }
            }
            $unavailable = [Activator]::CreateInstance($stateType,$true)
            foreach ($key in @('ChargeError','ThresholdError','PerformanceError','KeyboardBacklightError')) {
                $stateType.GetField($key,$flags).SetValue($unavailable,
                    'The optional device service is unavailable. Refresh after checking the installed components.')
            }
            $formType.GetMethod('DisplayState',$flags).Invoke($form,@($unavailable)) | Out-Null
            LayoutTree $form
            CheckTree $form
            if ($reserve.Enabled -or $autoDim.Enabled -or $restore.Enabled -or $backlightPanel.Controls.Count) {
                throw 'Unavailable keyboard must not expose writable controls'
            }
            $tabs.SelectedIndex = 0
            [System.Windows.Forms.Application]::DoEvents()
            LayoutTree $form
            CheckTree $form
            if ($thresholdPanel.Visible) { throw 'Unavailable threshold editor must be hidden on the active tab' }
            $formType.GetMethod('DisplayState',$flags).Invoke($form,@($state)) | Out-Null
            $tabs.SelectedIndex = 2
            if (-not $reserve.Enabled -or -not $restore.Enabled) {
                throw 'Controls must recover when the device becomes available'
            }
            Write-Output ($Culture + ' ' + $scenario.Name + ': PASS ' + $form.ClientSize)
        } finally { $bitmap.Dispose() }
    } finally { $form.Dispose() }
}
