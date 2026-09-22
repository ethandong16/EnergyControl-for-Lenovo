# EnergyControl for Lenovo

English | [简体中文](README.zh-CN.md)

[![CI](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ethandong16/EnergyControl-for-Lenovo?include_prereleases)](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

A portable Windows utility for charging, performance modes and keyboard backlight on compatible Lenovo laptops. One executable provides a Chinese desktop interface and a command-line interface.

Unofficial and community maintained. No Lenovo private DLLs, telemetry, background service, installer or automatic updater.

## Get Started

1. Download and extract the Windows x64 ZIP from [Releases](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases).
2. Compare its SHA-256 with the accompanying checksum file using `Get-FileHash .\<downloaded-file>.zip -Algorithm SHA256`.
3. Double-click `EnergyControl.exe` for the GUI, or run `.\EnergyControl.exe diagnose` in PowerShell.

Requires Windows 10/11 x64 and .NET Framework 4.8. Releases are unsigned. Source on `main` may contain changes not yet included in a release.

## Features and Dependencies

| Feature | Controls | Runtime dependency |
| --- | --- | --- |
| Charging | Normal, conservation, express | Compatible Lenovo EnergyDrv / ACPIVPC driver; optional Addin fallback |
| Charge thresholds (experimental) | Start and stop percentages | Lenovo Power RPC service and firmware support |
| Performance (experimental) | Auto, quiet, performance, geek/creator | Compatible installed Lenovo Addin |
| Keyboard backlight (experimental) | Off, level 1, level 2, auto; remember state, auto-dim, restore defaults | Compatible installed IdeaNotebookAddin and firmware |
| Diagnostics | Availability, states and errors | Available integrations are queried independently |

Settings vary by model. Conservation uses a firmware-defined limit and **does not imply arbitrary percentage support**. Automatic performance transition is also available through the CLI.

Backlight reads have been verified with IdeaNotebookAddin `1.0.13.79`: the observed device reported `TwoLevelsAuto` and no auto-dim support. Request construction is tested; hardware writes have not been verified across devices. See [compatibility](COMPATIBILITY.md) and [backlight interface notes](KEYBOARD_BACKLIGHT_INTERFACE.md).

## Desktop Interface

Battery, performance, keyboard and diagnostics have separate tabs. Refresh and status remain visible. Mode selectors reflect the last device read, and checkboxes represent backlight switches. Unsupported actions are disabled; unavailable threshold editors are hidden.

Changes require confirmation and are followed by a fresh device read. The diagnostics tab preserves full errors and the last read time. Switching tabs does not query hardware.

![Keyboard settings](docs/images/keyboard.png)

*Current source GUI with simulated device data for layout verification. Available controls depend on your machine.*

## Command Line

Read-only queries:

```powershell
.\EnergyControl.exe status
.\EnergyControl.exe diagnose
.\EnergyControl.exe charge direct get
.\EnergyControl.exe charge threshold get
.\EnergyControl.exe performance get
.\EnergyControl.exe keyboard-backlight get
.\EnergyControl.exe keyboard-backlight capability
```

Every write requires `--apply`. These are independent examples, not a sequence to run:

```powershell
.\EnergyControl.exe charge direct set conservation --apply
.\EnergyControl.exe charge threshold set 75 80 --apply
.\EnergyControl.exe performance set quiet --apply
.\EnergyControl.exe performance auto-transition on --apply
.\EnergyControl.exe keyboard-backlight set level1 --apply
.\EnergyControl.exe keyboard-backlight reserve on --apply
.\EnergyControl.exe keyboard-backlight auto-dim on --apply
.\EnergyControl.exe keyboard-backlight restore-default --apply
```

| Command | Accepted values |
| --- | --- |
| `charge direct set` or `charge set` | `normal`, `conservation`, `express` |
| `performance set` | `auto`, `quiet`, `performance`, `geek` |
| `keyboard-backlight set` | `off`, `level1`, `level2`, `auto` |
| `reserve`, `auto-dim`, `performance auto-transition` | `on`, `off` |

`charge get/set` uses the optional Addin; `charge direct get/set` uses EnergyDrv. `backlight` aliases `keyboard-backlight`. Run `help` for the command reference. Exit codes: `0` success, `1` operation failure or missing `--apply`, `2` invalid command or mode.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| IdeaNotebookAddin not found | Install or repair compatible Lenovo Vantage components. Baiying alone does not guarantee this Addin is installed. |
| Power RPC error `1722` | The required service is unavailable. Direct charging can still work independently. |
| EnergyDrv cannot be opened | Check the Lenovo ACPIVPC driver and access permissions. |
| Setting unavailable or rejected | Read diagnostics and check firmware support; do not infer support from another model. |

For integration testing, `LENOVO_SETTINGS_ADDIN_PATH` and `LENOVO_POWER_RPC_PATH` can point to trusted installed assemblies or their directories.

## Build and Verify

Use a .NET SDK capable of building SDK-style `net48` projects on Windows and PowerShell 7 for test scripts. Restore downloads public .NET Framework reference assemblies; compilation does not require Lenovo DLLs.

```powershell
.\build.ps1
.\tests\run-tests.ps1
.\verify-layout.ps1
```

The portable executable is written to `artifacts\publish\EnergyControl.exe`. Close running build outputs before rebuilding. `-Clean` removes previous build output.

Protocol and dependency tests run without Lenovo hardware. Real request construction is skipped when the Addin is absent. GUI tests use simulated states, check four tabs at narrow/wide sizes and 100/150/200% scaling, and save screenshots under `artifacts\layout`. Tests do not apply hardware settings.

| Source | Responsibility |
| --- | --- |
| `Gui.cs` | UI state, confirmations and asynchronous operations |
| `Gui.Layout.cs` | Tabs, controls and diagnostic presentation |
| `GuiDeviceClient.cs` | Device state and integration aggregation |
| `Program.cs` | CLI |
| `DirectChargeMode.cs`, `ChargeThreshold.cs`, `Models.cs`, `KeyboardBacklight.cs` | Device backends |

## Project

[Interfaces](INTERFACES.md) · [Charging research](REVERSE_ENGINEERING.md) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [Disclaimer](DISCLAIMER.md)

Licensed under [GPL-3.0-only](LICENSE). Lenovo is referenced solely to describe compatibility. This project is not affiliated with or endorsed by Lenovo.
