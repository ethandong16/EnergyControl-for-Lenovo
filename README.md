# EnergyControl for Lenovo

[![CI](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml)

EnergyControl for Lenovo is an unofficial, community-maintained Windows utility for reading and controlling charging modes on compatible Lenovo systems. The first public build is `v0.1.0-preview.1`.

EnergyControl for Lenovo 是一个非联想官方的 Windows 社区工具，用于读取和控制兼容联想设备的充电模式。首个公开版本为 `v0.1.0-preview.1`。

## Features / 功能

- Stable / 稳定：direct charging control through the installed Windows driver (`EnergyDrv`).
- Experimental / 实验：performance modes and custom start/stop percentage thresholds.
- One portable `EnergyControl.exe`; double-click opens the GUI, command-line arguments enable CLI mode.
- No telemetry, background service, installer, auto-update, or startup task.
- Lenovo Vantage / Commercial Vantage components are optional runtime integrations only.

直接驱动充电控制标记为稳定功能；性能控制和自定义百分比阈值属于实验功能。程序不包含 Lenovo 私有 DLL，也不会把它们复制到发布包中。

## Download / 下载

Download the unsigned preview ZIP from the [Releases](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases) page. Verify the accompanying `.sha256` file before running it.

预览版未签名，Windows SmartScreen 可能会显示警告。请只从 Release 页面下载，并使用随包提供的 SHA-256 校验文件核验。

## Requirements / 系统要求

- Windows 10 or Windows 11, x64.
- The system's built-in .NET Framework 4.8 runtime.
- A compatible Lenovo charging driver for direct control. Non-Lenovo systems remain read-only/unsupported.

## Usage / 使用

Double-click `EnergyControl.exe` for the GUI. The same file accepts CLI commands:

```text
EnergyControl.exe status
EnergyControl.exe diagnose
EnergyControl.exe charge direct get
EnergyControl.exe charge threshold get
EnergyControl.exe performance get

# Every write requires the explicit safety flag:
EnergyControl.exe charge direct set conservation --apply
EnergyControl.exe charge threshold set 75 80 --apply
EnergyControl.exe performance set performance --apply
```

写操作必须显式添加 `--apply`，否则程序不会打开驱动写入路径。

## Important limitations / 重要限制

- Firmware conservation mode is not the same thing as an arbitrary percentage threshold. A device may expose a fixed 80% maintenance mode while not supporting custom 75–85% values.
- Optional Vantage/Power RPC features can be unavailable, return RPC error `1722`, or be rejected by firmware. These failures do not disable the direct charging path.
- This project is not Lenovo software, is not endorsed by Lenovo, and uses no Lenovo logo or private binary in the repository or release ZIP.
- Driver writes can affect battery behavior. Use the GUI confirmation and read-only diagnostics first.

固件养护模式不等同于任意百分比阈值；设备可能只支持固定 80% 养护而不支持自定义 75–85%。Power RPC 缺失或返回 `1722` 时，直接充电功能仍会独立工作。

## Build from source / 从源码构建

The SDK-style project targets `.NET Framework 4.8` and restores the public `Microsoft.NETFramework.ReferenceAssemblies.net48` package at build time. No Lenovo DLL is needed to compile or run the read-only test suite.

```powershell
.\build.ps1 -Clean
.\tests\run-tests.ps1
.\verify-layout.ps1
```

The portable payload is copied to `artifacts\publish\EnergyControl.exe`. The optional Lenovo integrations are discovered from their installed system locations at runtime and are never packaged.

## Privacy and safety / 隐私与安全

EnergyControl has no telemetry, analytics, network client, or automatic update service. Diagnostics are local and read-only. Please report security issues privately according to [SECURITY.md](SECURITY.md), and read [DISCLAIMER.md](DISCLAIMER.md) before using driver writes.

## License / 许可证

GPL-3.0-only. See [LICENSE](LICENSE). “Lenovo” is used only to describe compatibility; all product names and trademarks belong to their respective owners.
