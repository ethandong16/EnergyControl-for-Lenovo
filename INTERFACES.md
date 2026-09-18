# EnergyControl for Lenovo 接口说明

本文记录 `v0.1.0-preview.1` 使用的本地协议与可选运行时接口。它不是 Lenovo 官方 SDK 文档；机型、固件和驱动升级都可能改变能力。

## 1. 直接充电协议（稳定功能）

程序打开 Windows 设备 `\\.\EnergyDrv`，通过 `DeviceIoControl` 调用 IOCTL `0x831020F8`。协议层只允许以下命令：

| 命令 | 含义 |
| --- | --- |
| `0xFF` | 读取状态标志 |
| `0x03` / `0x05` | 启用 / 关闭养护充电 |
| `0x07` / `0x08` | 启用 / 关闭快充 |
| `0x0D` / `0x0F` | 启用 / 关闭固定 80% 分支 |

状态标志解析：

| 位 | 含义 |
| --- | --- |
| `0x00000020` | 养护充电已启用 |
| `0x00000004` | 快充已启用 |
| `0x00020000` | 固件报告支持快充 |
| `0x00004000` | 固件报告支持固定 80% 分支 |

例如 `0x00860022` 表示养护充电已启用、快充能力已报告，但本标志值没有报告固定 80% 能力。程序不会把养护模式误报为任意百分比阈值。

驱动访问拆分为 `EnergyDriverProtocol`（标志解析、命令白名单、序列规划）和 `IEnergyDriverTransport`（Windows 设备传输）。测试使用模拟传输，不接触真实硬件。

## 2. Lenovo Addin（实验功能）

如果用户系统存在 `IdeaNotebookAddin.dll`，程序通过反射查找：

```text
IdeaNotebookAddin.IdeaNotebookAgent.GetInstance()
GetBatteryChargeMode()
SetBatteryChargeMode(request)
GetITSMode(request)
SetITSMode(request)
SetITSAutoTransition(request)
```

程序在运行时根据 setter 的参数类型创建请求对象，并按本地枚举映射 `Normal`、`Storage`、`Quick`、`ItsAuto`、`MmcCool`、`MmcPerformance` 和 `MmcGeek`。编译时不引用 `BatteryManagementContract.dll` 或 `PowerContract.dll`，发布包也不包含它们。

## 3. 自定义百分比阈值（实验功能）

程序可选地加载 `Lenovo.Vantage.PowerRpcClient.dll`，反射调用：

```text
ThinkPowerClient.RpcClient.ClientInitialize()
ClientGetChargeThreshold(slot, ref capable, ref enabled, ref start, ref stop)
ClientSetChargeThreshold(slot, start, stop)
```

常见 RPC 错误 `1722` 表示服务未运行。缺少客户端、RPC 异常、非联想设备或固件不支持时，GUI 会收起输入控件，直接充电功能继续可用。

## 4. JSON 兼容性

CLI 保留原有字段：

```json
{
  "slot": 0,
  "capable": true,
  "enabled": true,
  "startPercent": 75,
  "stopPercent": 80,
  "writable": true,
  "client": "Lenovo.Vantage.PowerRpcClient.dll 1.0.0.0"
}
```

Addin 响应仍按 `settingList` / `key` / `value` 读取。解析使用 .NET Framework 自带 `JavaScriptSerializer`；Malformed JSON 会安全降级，不会把异常响应当成能力支持。

## 5. CLI 安全语义

```text
EnergyControl.exe charge direct get
EnergyControl.exe charge threshold get
EnergyControl.exe performance get

EnergyControl.exe charge direct set conservation --apply
EnergyControl.exe charge threshold set 75 80 --apply
EnergyControl.exe performance set performance --apply
```

所有写命令都在解析参数后、打开驱动或加载可选 setter 前检查 `--apply`。直接驱动写入还会做能力检测、白名单校验、写后轮询和 GUI 二次确认。

## 6. 诊断

```powershell
.\bin\Release\net48\EnergyControl.exe diagnose
```

诊断输出 OS/进程位数、硬件摘要、直接驱动状态、Addin 发现结果和 Power RPC 可用性；诊断本身只读。
