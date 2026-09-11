# 联想百应：充电模式与性能管理接口

本目录的 Demo 不启动 `LenovoBaiying.exe`，也不创建 WebView2。它在当前用户
进程中直接加载联想已安装的 `IdeaNotebookAddin.dll`，调用与百应相同的设备代理。

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
.\bin\LenovoSettingsDemo.exe performance get
```

写命令必须显式带 `--apply`，否则 Demo 会在加载 Addin 和调用 setter 之前终止：

```powershell
.\bin\LenovoSettingsDemo.exe charge set normal --apply
.\bin\LenovoSettingsDemo.exe charge set conservation --apply
.\bin\LenovoSettingsDemo.exe charge set express --apply

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
  -> IdeaNotebookAddin.IdeaNotebookAgent.GetInstance()
  -> IdeaBatteryAgent.dll / IdeaPowerAgent.dll
  -> Lenovo 电源、固件和调度器驱动
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

## 旧 PowerRpcClient 接口

`Lenovo.Vantage.PowerRpcClient.dll` 搜索
`ncalrpc:BaseModuleRpcEndpoint_0` 到 `_9`。百应未启动时，本机
`ClientInitialize()` 返回 1722（`RPC_S_SERVER_UNAVAILABLE`），后续 getter
返回 1775。因此它不适合作为“无百应进程”方案。

IDA 恢复的旧接口 procedure：

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
