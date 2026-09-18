# Lenovo Settings Demo

[![CI](https://github.com/ethandong16/LenovoSettingsDemo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/LenovoSettingsDemo/actions/workflows/ci.yml)

一个 Windows/.NET Framework 演示程序。充电模式可直接访问 Lenovo
`EnergyDrv` 驱动，不需要启动 Vantage 或联想百应；性能模式仍复用本机安装的
`IdeaNotebookAddin`。部分机型的自定义百分比阈值使用独立的 Power RPC 接口。

> 这不是 Lenovo 官方 SDK，也不是 Lenovo 官方产品。底层 Addin 是未公开、可能变化的
> 私有接口。程序只会展示本机报告的能力，写入仍可能被 BIOS、固件或驱动拒绝。

![GUI preview](gui-preview.png)

## 功能

- 充电模式：常规、养护、快速充电，实际选项以设备返回值为准。
- 直接驱动充电后端：绕过 Vantage/百应进程和 RPC，并显示原始能力标志。
- 自定义充电阈值：读取是否支持、启用状态、起充/停充百分比，并可设置新阈值。
- 性能模式：自动、安静/节能、高性能、极客/创作模式，按设备能力动态显示。
- 充电模式、自定义阈值和性能模式独立检测，兼容无电池、部分接口缺失和只读设备。
- 支持消费版及商用 Vantage 的常见 Addin 路径、能力别名和不同分隔符。
- 写入前重新检查能力，要求确认并验证 setter 错误码及修改后状态。
- 提供 CLI 诊断和只读状态输出。

## 要求

- Windows x64
- .NET Framework 4.7.2 或更高版本
- 已安装 Lenovo Vantage、Commercial Vantage 或联想百应及对应设备驱动
- Windows PowerShell 5.1 或 PowerShell 7

## 构建

```powershell
.\build.ps1 -Clean
```

构建脚本从本机 Lenovo Addin 安装目录复制所需 DLL 到 `bin`。这些 Lenovo 二进制文件
不属于本仓库，也不会提交到 Git。

如果自动发现失败，可指定包含 `IdeaNotebookAddin.dll` 的目录：

```powershell
.\build.ps1 -Clean -AddinPath 'D:\LenovoAddin\1.0.13.79'
```

## 使用

```powershell
.\bin\LenovoSettingsGui.exe

.\bin\LenovoSettingsDemo.exe diagnose
.\bin\LenovoSettingsDemo.exe status
.\bin\LenovoSettingsDemo.exe charge get
.\bin\LenovoSettingsDemo.exe charge direct get
.\bin\LenovoSettingsDemo.exe charge direct set conservation --apply
.\bin\LenovoSettingsDemo.exe charge threshold get
.\bin\LenovoSettingsDemo.exe charge threshold set 75 80 --apply
.\bin\LenovoSettingsDemo.exe performance get
```

自定义阈值示例表示“低于 75% 开始充电，充到 80% 停止”。该功能依赖部分
Vantage/百应组件提供的 Power RPC 服务；若返回错误码 `1722`，请先启动 Lenovo
Vantage 或联想百应。并非所有机型、固件或电池都支持自定义阈值。

“养护模式”与“任意百分比阈值”不是同一接口。直接驱动后端只能使用固件定义的
养护策略；只有 `storage80Capable=true` 时才能确认该策略属于固定 80% 版本。
逆向依据和控制码记录见 [REVERSE_ENGINEERING.md](REVERSE_ENGINEERING.md)。

CLI 写操作必须显式添加 `--apply`。完整接口说明见
[INTERFACES.md](INTERFACES.md)，产品线差异和兼容性调查见
[COMPATIBILITY.md](COMPATIBILITY.md)。

## 验证

```powershell
.\verify-layout.ps1
.\verify-layout.ps1 -LiveRead
```

验证脚本覆盖常见窗口宽度、150%/200% DPI、能力别名、响应降级和真实设备只读状态。

## 持续集成

GitHub Actions 会在 `main` 推送、Pull Request 和手动触发时，在 Windows Runner 上
自动编译 CLI 与 GUI，并测试无 Lenovo Addin 时的安全降级路径。由于 Lenovo Contract
DLL 是本机闭源组件，CI 使用 `ci/` 中的最小类型签名桩进行编译验证，不生成可分发的
运行包。部署版本请在目标 Lenovo 电脑上运行 `build.ps1` 构建。
