# Lenovo keyboard backlight interface reconnaissance

Evidence collected from the locally installed Lenovo components on September 21, 2026.

## Conclusion

The usable control entry point is Lenovo Vantage's `IdeaNotebookAddin`, not the
`IdeaKBDManagerAddin` or the Lenovo Baiying shell itself.

Primary assemblies:

```text
C:\ProgramData\Lenovo\Vantage\Addins\IdeaNotebookAddin\1.0.13.79\IdeaNotebookAddin.dll
C:\ProgramData\Lenovo\Vantage\Addins\IdeaNotebookAddin\1.0.13.79\KeyboardContract.dll
```

The installed package metadata describes `IdeaNotebookAddin` as controlling
power, battery, and keyboard features for Idea/Lenovo notebooks.

## Managed entry points

Create the singleton agent through:

```csharp
var agentType = addinAssembly.GetType("IdeaNotebookAddin.IdeaNotebookAgent");
var agent = agentType.GetMethod("GetInstance").Invoke(null, null);
```

Read-only methods:

```text
bool IsSupportBacklight()
CapabilityResponse GetCapabilityResponse()
KeyboardSettingsResponse GetBacklightStatus()
KeyboardSettingsResponse GetKeyboardSettings()
```

Write/default methods exposed by the same agent:

```text
CommonResponse SetBacklightStatus(KeyboardSettingsRequest request)
bool SetBacklightReserve(bool save)
bool SetBacklightAutoDim(bool enable)
void KeyboardBacklightRestoreDefault()
```

The internal handler also exposes the lower-level service contract methods:

```text
Response GetBacklight(string payload, Action<Response> progressCallback)
Response SetBacklight(string payload, Action<Response> progressCallback)
Response SetBacklightReserve(string payload, Action<Response> progressCallback)
Response SetBacklightAutoDim(string payload, Action<Response> progressCallback)
```

## Contract model

`KeyboardContract.dll` contains:

```text
Lenovo.Modern.Contracts.Keyboard.KeyboardSettingsRequest
Lenovo.Modern.Contracts.Keyboard.KeyboardSettingsResponse
Lenovo.Modern.Contracts.Keyboard.SettingList
Lenovo.Modern.Contracts.Keyboard.Setting
Lenovo.Modern.Contracts.Keyboard.CapabilityResponse
```

The response shape observed on this machine is:

```json
{
  "List": {
    "Items": [
      {
        "key": "KeyboardBacklightStatus",
        "value": "Level_2",
        "enabled": 0,
        "errorCode": "Success",
        "ErrorCodeJson": 0
      }
    ]
  }
}
```

Relevant setting keys:

```text
KeyboardBacklightControl
KeyboardBacklightStatus
KeyboardBacklightReserve
KeyboardBacklightLevel
KeyboardBacklightAutoDimCapability
KeyboardBacklightAutoDimStatus
KeyboardBacklightTimeOut
KeyboardBacklightTimeOutLevel
```

Relevant command names:

```text
Get-Backlight
Get-BacklightOnSystemChange
Set-Backlight
Set-BacklightReserve
Set-BacklightAutoDim
Set-BacklightTimeOutStatus
```

Enum values recovered from `KeyboardContract.dll`:

```text
BacklightLevelType:     Off=0, Level_1=1, Level_2=2, Auto=3, DisabledOff=4
BacklightMaxLevelType:  NoCapability=0, OneLevel=1, TwoLevels=2,
                        TwoLevelsRedWhite=3, TwoLevelsAuto=4
BacklightTimeOutLevelType:
                        ThirtySeconds=0, OneMinute=1, FiveMinutes=2, Never=3
CapabilitiesType:       None=0, KeyboardBacklight_OneLevel=1,
                        KeyboardBacklight_TwoLevels=2, FnLock=4
```

The project now constructs the public setter request at runtime, without
referencing or packaging Lenovo's private contract assembly at compile time:

```csharp
var request = new KeyboardSettingsRequest
{
    List = new SettingList
    {
        Items = new List<Setting>
        {
            new Setting
            {
                key = "KeyboardBacklightStatus",
                value = "Level_1"
            }
        }
    }
};

var result = agent.SetBacklightStatus(request);
```

The implementation lives in `KeyboardBacklight.cs` and is exposed through both
CLI and WinForms GUI. The integration tests validate the command mapping and
request construction without sending a hardware write. CLI writes remain gated
behind `--apply`, and GUI writes require a confirmation dialog.

Supported CLI operations:

```text
EnergyControl.exe keyboard-backlight get
EnergyControl.exe keyboard-backlight set off|level1|level2|auto --apply
EnergyControl.exe keyboard-backlight reserve on|off --apply
EnergyControl.exe keyboard-backlight auto-dim on|off --apply
EnergyControl.exe keyboard-backlight restore-default --apply
```

## Driver path and IOCTL clues

`IdeaNotebookAddin.dll` opens:

```text
\\.\EnergyDrv
```

The relevant constants recovered from `IdeaNotebookAddin.Const` are:

```text
IoctlEnergydrvKblccontrol = 0x83102144
IoctlEnergydrvKblrcontrol = 0x83102154
IoctlEnergydrvKblacontrol = 0x83102158
IoctlEnergydrvRegArmKblEvent = 0x83102160
IoctlEnergydrvUnRegArmKblEvent = 0x83102164
BacklightOn = 1
BacklightOff = 0
```

The log strings identify the operations behind the KBL control path:

```text
KBLC_GET_CAPABILITY
KBLC_GET_STATUS
KBLC_SET_STATUS
KBLR        (backlight reserve)
KBLA        (backlight auto-dim)
```

The same assembly contains `NativeHelper.DoDeviceIoControl(...)` overloads and
`KblEventNativeHelper.DeviceIoControl(...)` for status-change notifications.

## Current machine read-only result

The agent was loaded successfully and returned:

```json
{
  "isSupportBacklight": true,
  "capabilities": [
    { "key": "KeyboardBacklightControl", "value": "True" },
    { "key": "PrimeKey", "value": "True" },
    { "key": "FnLock", "value": "True" }
  ],
  "status": [
    { "key": "KeyboardBacklightLevel", "value": "TwoLevelsAuto" },
    { "key": "KeyboardBacklightStatus", "value": "Level_2" },
    { "key": "KeyboardBacklightReserve", "value": "False" },
    { "key": "IsCVDSP", "value": "False" },
    { "key": "KeyboardBacklightAutoDimCapability", "value": "False" },
    { "key": "KeyboardBacklightAutoDimStatus", "value": "NoCapability" }
  ]
}
```

## Vantage versus Lenovo Baiying

`IdeaKBDManagerAddin.dll` is a separate hotkey/layout addin. Its useful methods
are `GetKBDLayoutOptions`, `GetKeyboardVersion`, `GetKeyboardLayout`, and hotkey
OSD handlers; it does not expose the backlight setter found in
`IdeaNotebookAddin`.

The installed Lenovo Baiying binaries expose the generic Vantage RPC transport:

```text
Lenovo.Vantage.AbstractClient.MakeRequest(endpoint, request, ...)
Lenovo.Vantage.AbstractClient.MakeRawRequest(endpoint, request, ...)
Lenovo.Vantage.RpcClient.MakeRequest(request, ...)
LenovoBaiying.Interfaces.Interfaces.ShellInterfaces.IRpcService.AddinRequestAsync(...)
```

No independent `KeyboardBacklight` method was found in `LenovoBaiying.dll` or
`LenovoBaiying.Interfaces.dll`. The practical path is therefore:

```text
Baiying/Vantage shell -> Vantage RPC transport -> IdeaNotebookAddin
                       -> KeyboardHandler -> \\.\EnergyDrv
```
