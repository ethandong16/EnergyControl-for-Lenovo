# EnergyControl for Lenovo

English | [简体中文](README.zh-CN.md)

[![CI](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ethandong16/EnergyControl-for-Lenovo?include_prereleases)](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases)
[![License](https://img.shields.io/github/license/ethandong16/EnergyControl-for-Lenovo)](LICENSE)

EnergyControl for Lenovo is an unofficial, community-maintained Windows utility for reading and controlling charging modes on compatible Lenovo systems. The current public preview is `v0.1.0-preview.1`.

## Features

- **Stable:** direct charging control through the installed Windows driver (`EnergyDrv`).
- **Experimental:** performance modes and custom start/stop percentage thresholds.
- A single portable `EnergyControl.exe`: double-click it for the GUI or pass arguments for CLI mode.
- No telemetry, background service, installer, automatic updater, or startup task.
- Lenovo Vantage and Commercial Vantage components are optional runtime integrations only.
- No Lenovo private DLL is committed to the repository or included in a release package.

## Download

Download the unsigned Windows x64 preview ZIP from the [Releases](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases) page and verify its accompanying `.sha256` file before running it.

The preview is not code-signed, so Windows SmartScreen may display a warning.

## Requirements

- Windows 10 or Windows 11, x64.
- The system-provided .NET Framework 4.8 runtime.
- A compatible Lenovo charging driver for direct control. Non-Lenovo and unsupported systems safely report the feature as unavailable.

## Usage

Double-click `EnergyControl.exe` to open the GUI. The same executable supports CLI commands:

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

Without `--apply`, a write command exits before opening a driver write path or invoking an optional setter.

## Important limitations

- Firmware conservation mode is not the same as an arbitrary percentage threshold. A device may expose a fixed 80% maintenance mode without supporting custom values such as 75–85%.
- Optional Vantage or Power RPC features may be unavailable, return RPC error `1722`, or be rejected by firmware. These failures do not disable the direct charging path.
- Driver writes can affect battery behavior. Run read-only diagnostics first and review the GUI confirmation before applying a change.
- This project is not Lenovo software, is not endorsed by Lenovo, and does not use a Lenovo logo or distribute Lenovo private components.

See [COMPATIBILITY.md](COMPATIBILITY.md) for device-family differences and [INTERFACES.md](INTERFACES.md) for protocol details.

## Build from source

The SDK-style project targets .NET Framework 4.8 and restores the public `Microsoft.NETFramework.ReferenceAssemblies.net48` package at build time. Lenovo DLLs are not required to compile the project or run its automated tests.

```powershell
.\build.ps1 -Clean
.\tests\run-tests.ps1
.\verify-layout.ps1
```

The portable executable is copied to `artifacts\publish\EnergyControl.exe`. Optional Lenovo integrations are discovered from installed system locations at runtime and are never packaged by the build.

## Privacy and security

EnergyControl has no telemetry, analytics, network client, or automatic update service. Diagnostics are local and read-only.

Report security issues privately according to [SECURITY.md](SECURITY.md), and read [DISCLAIMER.md](DISCLAIMER.md) before using driver writes.

## Contributing

Contributions and compatibility reports are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Do not upload Lenovo private binaries, firmware images, or personal diagnostic data.

## License

EnergyControl for Lenovo is licensed under `GPL-3.0-only`. See [LICENSE](LICENSE).

“Lenovo” is used only to describe compatibility. All product names and trademarks belong to their respective owners.
