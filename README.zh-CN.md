# EnergyControl for Lenovo

[English](README.md) | 简体中文

[![CI](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ethandong16/EnergyControl-for-Lenovo?include_prereleases)](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases)
[![License](https://img.shields.io/github/license/ethandong16/EnergyControl-for-Lenovo)](LICENSE)

EnergyControl for Lenovo 是一个非联想官方、由社区维护的 Windows 工具，用于读取和控制兼容联想设备的充电模式。当前公开预览版本为 `v0.1.0-preview.1`。

## 功能

- **稳定功能：**通过系统已安装的 Windows 驱动（`EnergyDrv`）直接控制充电模式。
- **实验功能：**性能模式、自定义起充和停充百分比阈值，以及联想键盘背光控制。
- 仅需一个便携版 `EnergyControl.exe`：双击进入 GUI，携带参数时进入 CLI。
- 不包含遥测、后台服务、安装器、自动更新或开机启动任务。
- Lenovo Vantage 和 Commercial Vantage 组件仅作为可选的运行时集成。
- 仓库和发布包均不包含 Lenovo 私有 DLL。

## 下载

请从 [Releases](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases) 页面下载 Windows x64 预览版 ZIP，并在运行前使用随附的 `.sha256` 文件进行校验。

当前预览版没有代码签名，因此 Windows SmartScreen 可能显示警告。

## 系统要求

- Windows 10 或 Windows 11，x64。
- 系统内置的 .NET Framework 4.8 运行时。
- 直接控制需要兼容的 Lenovo 充电驱动。非 Lenovo 或不受支持的设备会安全地显示功能不可用。

## 使用方法

双击 `EnergyControl.exe` 启动 GUI。同一个可执行文件也支持 CLI 命令：

```text
EnergyControl.exe status
EnergyControl.exe diagnose
EnergyControl.exe charge direct get
EnergyControl.exe charge threshold get
EnergyControl.exe performance get
EnergyControl.exe keyboard-backlight get
EnergyControl.exe keyboard-backlight capability

# 所有写操作都必须显式提供安全参数：
EnergyControl.exe charge direct set conservation --apply
EnergyControl.exe charge threshold set 75 80 --apply
EnergyControl.exe performance set performance --apply
EnergyControl.exe keyboard-backlight set level1 --apply
EnergyControl.exe keyboard-backlight reserve on --apply
EnergyControl.exe keyboard-backlight auto-dim on --apply
EnergyControl.exe keyboard-backlight restore-default --apply
```

如果缺少 `--apply`，写命令会在打开驱动写入路径或调用可选 setter 之前终止。

## 重要限制

- 固件养护模式不等同于任意百分比阈值。设备可能支持固定 80% 养护，但不支持 75–85% 等自定义范围。
- Vantage 或 Power RPC 实验功能可能不可用、返回 RPC 错误 `1722`，或被固件拒绝。这些故障不会影响直接充电路径。
- 键盘背光控制使用 Lenovo Vantage 或联想百应安装的 `IdeaNotebookAddin`。可用档位和写入方法取决于机型与固件；只有设备报告支持自动调暗时，`auto-dim` 才会启用。
- 驱动写入会影响电池行为。应用更改前请先运行只读诊断，并仔细确认 GUI 提示。
- 本项目不是 Lenovo 官方软件，未获得 Lenovo 背书，不使用 Lenovo Logo，也不分发 Lenovo 私有组件。

设备产品线差异请参阅 [COMPATIBILITY.md](COMPATIBILITY.md)，协议细节请参阅 [INTERFACES.md](INTERFACES.md)。

## 从源码构建

项目采用 SDK 风格，目标为 .NET Framework 4.8。构建时会还原公开的 `Microsoft.NETFramework.ReferenceAssemblies.net48` 包。编译项目和运行自动测试均不需要 Lenovo DLL。

```powershell
.\build.ps1 -Clean
.\tests\run-tests.ps1
.\verify-layout.ps1
```

便携版可执行文件会被复制到 `artifacts\publish\EnergyControl.exe`。可选 Lenovo 组件只会在运行时从系统安装位置发现，不会被构建脚本打包。

## 隐私与安全

EnergyControl 不包含遥测、分析、网络客户端或自动更新服务。诊断功能只在本地运行且只读。

请按照 [SECURITY.md](SECURITY.md) 私下报告安全问题，并在使用驱动写入功能前阅读 [DISCLAIMER.md](DISCLAIMER.md)。

## 参与贡献

欢迎提交代码和兼容性报告。创建 Pull Request 前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。请勿上传 Lenovo 私有二进制文件、固件镜像或个人诊断数据。

## 许可证

EnergyControl for Lenovo 使用 `GPL-3.0-only` 许可证，详见 [LICENSE](LICENSE)。

“Lenovo”仅用于说明兼容目标。所有产品名称和商标均归其各自所有者所有。
