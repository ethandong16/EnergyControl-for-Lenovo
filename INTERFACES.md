# 联想百应：充电模式与性能管理接口

本目录的 Demo 不主动启动 `LenovoBaiying.exe`，也不创建 WebView2。GUI 的充电模式
优先直接访问 `\\.\EnergyDrv`，失败后才回退到 `IdeaNotebookAddin.dll`；性能模式使用
Addin。自定义充电阈值使用 Power RPC，因此需要对应的 Vantage/百应服务端运行。

不同产品线的能力并不统一，产品线调查、命名别名和降级策略见
[`COMPATIBILITY.md`](COMPATIBILITY.md)。GUI 和 CLI 都以本机返回的
`Supported-*` 为准，不会把未报告的模式当成可写能力。

> 这些接口不是联想公开、稳定的第三方 SDK。Addin 升级后程序集版本、类型名或行为
> 可能变化。写入前应先读取能力与当前状态。

## 快速使用

环境要求：Windows x64、.NET Framework 4.7.2 或更高版本，以及已安装：

```text
C:\ProgramData\Lenovo\Vantage\Addins\IdeaNotebookAddin\<version>
```

构建脚本自动选择版本号最高的目录，并把 Addin 的托管及原生 DLL 复制到 `bin`：

```powershell
cd 'LenovoSettingsDemo'
.\build.ps1 -Clean
.\bin\LenovoSettingsDemo.exe status

# 输出 Addin 版本、架构和关键方法是否存在
.\bin\LenovoSettingsDemo.exe diagnose
```

如果 Addin 来自离线镜像或商用 Vantage，可显式指定包含
`IdeaNotebookAddin.dll` 的目录：

```powershell
.\build.ps1 -Clean -AddinPath 'D:\LenovoAddin\1.0.13.79'
```

运行已构建程序时，也可用 `LENOVO_SETTINGS_ADDIN_PATH` 指向 Addin 文件或目录；
设置该变量后程序不会回退到系统安装目录，适合企业镜像和兼容性测试。

窗口版：

```powershell
.\bin\LenovoSettingsGui.exe
```

GUI 启动后自动读取状态，只显示本机报告支持的模式。点击模式按钮后还会显示确认
对话框；选择“否”不会调用 setter。充电和性能分开读取，某一项不可用时另一项仍可用。

只读命令：

```powershell
.\bin\LenovoSettingsDemo.exe charge get
.\bin\LenovoSettingsDemo.exe charge direct get
.\bin\LenovoSettingsDemo.exe charge threshold get
.\bin\LenovoSettingsDemo.exe performance get
```

写命令必须显式带 `--apply`，否则 Demo 会在加载 Addin 和调用 setter 之前终止：

```powershell
.\bin\LenovoSettingsDemo.exe charge set normal --apply
.\bin\LenovoSettingsDemo.exe charge set conservation --apply
.\bin\LenovoSettingsDemo.exe charge set express --apply
.\bin\LenovoSettingsDemo.exe charge direct set normal --apply
.\bin\LenovoSettingsDemo.exe charge direct set conservation --apply
.\bin\LenovoSettingsDemo.exe charge direct set express --apply
.\bin\LenovoSettingsDemo.exe charge threshold set 75 80 --apply

.\bin\LenovoSettingsDemo.exe performance set auto --apply
.\bin\LenovoSettingsDemo.exe performance set quiet --apply
.\bin\LenovoSettingsDemo.exe performance set performance --apply
.\bin\LenovoSettingsDemo.exe performance set geek --apply
.\bin\LenovoSettingsDemo.exe performance auto-transition on --apply
.\bin\LenovoSettingsDemo.exe performance auto-transition off --apply
```

## 调用架构

```text
LenovoSettingsDemo.exe
  -> CreateFile("\\.\EnergyDrv")
  -> DeviceIoControl(0x831020F8)
  -> Lenovo ACPIVPC / EC firmware

LenovoSettingsDemo.exe
  -> IdeaNotebookAddin.IdeaNotebookAgent.GetInstance()
  -> IdeaBatteryAgent.dll / IdeaPowerAgent.dll
  -> Lenovo 电源、固件和调度器驱动

LenovoSettingsDemo.exe
  -> Lenovo.Vantage.PowerRpcClient.dll
  -> ncalrpc:BaseModuleRpcEndpoint_0 ... _9
  -> Lenovo Vantage / 百应电源服务
```

`IdeaNotebookAgent` 本身是程序集内部类型，但其下列业务方法是 public。Demo 用反射
取得该单例和方法，setter 参数仍使用公开的强类型 Contract 对象，不伪造调用进程，
也不修改 VantageService 的信任校验。

## 充电模式

百应服务层合约：

```text
Contract: SystemManagement.BatteryMgmt
Get-Capability
Get-BatteryChargeMode
Set-BatteryChargeMode
```

直接 Addin 方法：

```csharp
object agent = IdeaNotebookAgent.GetInstance(); // Demo 通过反射执行
BatteryMgmtResponse GetBatteryChargeMode();
BatteryMgmtResponse SetBatteryChargeMode(BatteryMgmtRequest request);
```

请求类型与枚举位于
`Lenovo.Modern.Contracts.BatteryManagement`（`BatteryManagementContract.dll`）：

```csharp
new BatteryMgmtRequest {
    BatteryChargeMode = BatteryChargeModeType.Storage
};
```

等价序列化 payload：

```json
{"settingList":[{"key":"BatteryChargeMode","value":"Storage"}]}
```

| Demo 名称 | 合约值 | `BatteryChargeModeType` | 能力位 |
| --- | --- | ---: | ---: |
| `normal` | `Normal` | 0 | 1 |
| `conservation` / `storage` | `Storage` | 1 | 2 |
| `express` / `quick` | `Quick` | 2 | 4 |

### 直接驱动协议

`charge direct ...` 不加载 Vantage/百应 DLL。它使用逆向得到的 EnergyDrv 协议：

| 操作 | 输入命令 | 状态/能力位 |
| --- | ---: | ---: |
| 查询 | `0xFF` | 返回 32 位 flags |
| 开启/关闭养护 | `0x03` / `0x05` | 状态 `0x20` |
| 开启/关闭快充 | `0x07` / `0x08` | 状态 `0x04`，能力 `0x20000` |
| 固定 80% 扩展开启/关闭 | `0x0D` / `0x0F` | 能力 `0x4000` |

固定 80% 扩展命令只会在固件返回 `0x4000` 能力位时发送。没有该位时，“养护”是
固件预设策略，Demo 不推断其百分比。完整证据见
[`REVERSE_ENGINEERING.md`](REVERSE_ENGINEERING.md)。

本机只读实测结果：

```json
{
  "settingList": [
    {"key":"Supported-BatteryChargeMode","value":"Normal,Storage,Quick"},
    {"key":"BatteryChargeMode","value":"Normal"}
  ]
}
```

## 性能管理（ITS / DYTC）

百应服务层合约：

```text
Contract: SystemManagement.Power
Get-Capability
Get-ITSMode
Set-ITSMode
Set-ITSAutoTransition
```

直接 Addin 方法：

```csharp
PowerSettingsResponse GetITSMode(PowerSettingsRequest request);
PowerSettingsResponse SetITSMode(PowerSettingsRequest request);
PowerSettingsResponse SetITSAutoTransition(PowerSettingsRequest request);
```

请求类型与枚举位于 `Lenovo.Modern.Contracts.Power`（`PowerContract.dll`）：

```csharp
// 读取时声明界面支持极客选项，否则 Addin 会过滤 MMC_Geek。
new PowerSettingsRequest {
    UISupportGeekMode = true
};

new PowerSettingsRequest {
    ItsMode = ItsModeType.MmcPerformance,
    UISupportGeekMode = true
};

new PowerSettingsRequest {
    IsAutoTransitionEnabled = true
};
```

`SetITSMode` 的等价序列化 payload：

```json
{
  "settingList": [
    {"key":"ITSMode","value":"MMC_Performance"},
    {"key":"UISupportGeekMode","value":"True"}
  ]
}
```

| Demo 名称 | 序列化值 | `ItsModeType` | `SupportedMmcModeType` 能力位 |
| --- | --- | ---: | ---: |
| `auto` | `ITS_Auto` | 1 | 1 |
| `quiet` / `cool` | `MMC_Cool` | 2 | 2 |
| `performance` | `MMC_Performance` | 3 | 8 |
| `geek` | `MMC_Geek` | 4 | 16 |

`SupportedMmcModeType` 是位掩码，不是 `ItsModeType` 的数值。`ShowBsmAsQuietBsm`
决定 UI 将 `MMC_Cool` 显示为安静/冷却还是节能；`ShowGeekAsCreator` 决定是否
显示为创作模式。调用 `GetITSMode` 时必须传 `UISupportGeekMode = true`，
Addin 才会把 `MMC_Geek` 和 `IsGeekOptionGrey` 加入响应。

本机只读实测结果：

```json
{
  "settingList": [
    {"key":"IsDispatcher","value":"True"},
    {"key":"ShowBsmAsQuietBsm","value":"False"},
    {"key":"ShowGeekAsCreator","value":"False"},
    {"key":"WorkingDriver","value":"dispatcher"},
    {"key":"Supported-ITSMode","value":"MMC_Auto,MMC_Cool,MMC_Performance,MMC_Geek"},
    {"key":"ITSMode","value":"MMC_Performance"},
    {"key":"QuietPerformanceMode","value":"NotSupport"},
    {"key":"IsGeekOptionGrey","value":"False"},
    {"key":"DispatcherVersion","value":"3"},
    {"key":"ErrorCode","value":"0"}
  ]
}
```

## 为什么不直接调用 VantageService RPC

公开客户端可连接 endpoint
`8EEFA2E8-D033-4A08-A484-139C0B09371D`，请求 envelope 形如：

```json
{
  "contract": "SystemManagement.BatteryMgmt",
  "command": "Get-BatteryChargeMode",
  "payload": "",
  "targetAddin": "IdeaNotebookAddin",
  "callerPid": 0
}
```

但服务端 `VantageService.RequestMade -> Result(request, callerPid)` 会调用
`ProcTrustedStatusHelper.IsTrusted`。未签名的独立 Demo 实测返回：

```json
{
  "type": 0,
  "percentage": 0,
  "errorcode": 403,
  "errordesc": "Service do not trust the connecting Client"
}
```

因此本 Demo 不使用该 RPC 路径，也不伪造 `callerPid` 或绕过签名/许可证校验。

## 自定义充电阈值（PowerRpcClient）

`Lenovo.Vantage.PowerRpcClient.dll` 搜索
`ncalrpc:BaseModuleRpcEndpoint_0` 到 `_9`。Demo 会优先加载程序目录中的客户端，
找不到时再从已安装的 Vantage Addin 中定位最高版本。接口签名为：

```csharp
int ClientGetChargeThreshold(
    int slotnum,
    out bool iscapable,
    out bool isenabled,
    out int startval,
    out int stopval);

int ClientSetChargeThreshold(int slotnum, int startVal, int stopVal);
```

- `startval`：电量低于该百分比后开始充电。
- `stopval`：电量达到该百分比后停止充电。
- Demo 当前操作内置电池槽位 `0`，要求两个值位于 `0..100` 且
  `startVal < stopVal`。
- 写入前读取 `iscapable`，不支持、只读或 RPC 返回非零错误码时不会继续写入。
- CLI 写入仍必须显式带 `--apply`；GUI 写入前显示确认对话框，写入后重新读取校验。

百应/Vantage 服务端未启动时，本机 `ClientInitialize()` 返回 1722
（`RPC_S_SERVER_UNAVAILABLE`）。Demo 不会自行启动服务，只会将阈值功能显示为局部
不可用；充电模式和性能模式仍可继续使用。

IDA 恢复的接口 procedure：

| 方法 | Procedure |
| --- | ---: |
| `ClientGetChargeThreshold` | `0x23` |
| `ClientSetChargeThreshold` | `0x28` |
| `ClientGetSmartCharge` | `0x50` |
| `ClientSetSmartCharge` | `0x51` |
| `ClientGetIntelligentCoolingAutoMode` | `0x15` |
| `ClientGetIntelligentCooling` | `0x16` |
| `ClientGetCoolMode` | `0x17` |
| `ClientSetIntelligentCoolingAutoMode` | `0x1D` |
| `ClientSetIntelligentCooling` | `0x1E` |
| `ClientSetCoolMode` | `0x1F` |

## 安全与兼容性

- Demo 不带 `--apply` 时不会执行任何 setter。
- setter 会打印修改前、返回值和修改后状态。
- 应先根据 `Supported-BatteryChargeMode` / `Supported-ITSMode` 判断本机能力。
- 已知枚举仍可能被当前 BIOS、固件或驱动拒绝，应以响应的 `ErrorCode` 为准。
- Addin 更新后请重新运行 `build.ps1 -Clean`，不要混用不同版本的 Contract DLL。
