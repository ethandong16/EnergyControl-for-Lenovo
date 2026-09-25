# EnergyControl for Lenovo

[English](README.md) | 简体中文 | [日本語](README.ja.md)

[![CI](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ethandong16/EnergyControl-for-Lenovo?include_prereleases)](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

EnergyControl for Lenovo 是一款面向兼容联想笔记本的非官方 Windows 便携工具，提供图形界面和命令行，用于管理充电模式、性能模式、充电阈值和键盘背光。

实际可用控件取决于机型、固件、驱动和本机安装的 Lenovo 组件。本程序不包含 Lenovo 私有 DLL、遥测、后台服务、安装器或自动更新。

## 快速开始

1. 从 [Releases](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases) 下载并解压 Windows x64 ZIP。
2. 使用 `Get-FileHash .\<下载的文件>.zip -Algorithm SHA256` 校验文件。
3. 运行 `EnergyControl.exe` 打开图形界面，或在 PowerShell 中使用命令行。

需要 Windows 10/11 x64 和 .NET Framework 4.8。发布包是未签名的预览版本。

## 功能

| 功能 | 示例 | 可用条件 |
| --- | --- | --- |
| 充电模式 | 常规、养护、快充 | 兼容的 EnergyDrv / ACPIVPC 驱动或 Lenovo Addin |
| 充电阈值 | 起充和停充百分比 | 可选 Lenovo Power RPC 服务及固件支持 |
| 性能模式 | 自动、安静、高性能、极客 | 兼容的 Lenovo Addin |
| 键盘背光 | 关闭、一级、二级、自动 | 兼容的 IdeaNotebookAddin 及固件 |
| 诊断 | 状态、能力和错误 | 对可用接口进行只读检查 |

养护模式使用固件定义的限制，不代表支持任意百分比。不可用功能会保持禁用，不会阻止其他功能运行。

## 图形界面

GUI 启动时跟随 Windows 显示语言：`zh-*` 使用简体中文，`ja-*` 使用日文，其余语言使用英文。帮助按钮会以当前语言打开内置的 `README.html`。界面包含电池、性能、键盘和诊断四个标签页。

修改设置需要确认，应用后会重新读取设备状态。不支持的操作会禁用，不可用的充电阈值控件会隐藏。

![键盘设置](docs/images/keyboard.png)

截图使用模拟设备数据进行布局验证，实际控件取决于本机能力。

## 命令行

只读示例：

```powershell
.\EnergyControl.exe status
.\EnergyControl.exe diagnose
.\EnergyControl.exe charge direct get
.\EnergyControl.exe charge threshold get
.\EnergyControl.exe performance get
.\EnergyControl.exe keyboard-backlight get
```

所有写入都需要 `--apply`：

```powershell
.\EnergyControl.exe charge direct set conservation --apply
.\EnergyControl.exe charge threshold set 75 80 --apply
.\EnergyControl.exe performance set quiet --apply
.\EnergyControl.exe keyboard-backlight set level1 --apply
.\EnergyControl.exe keyboard-backlight restore-default --apply
```

可用模式包括 `normal`、`conservation`、`express`、`auto`、`quiet`、`performance`、`geek`、`off`、`level1` 和 `level2`。运行 `EnergyControl.exe help` 查看完整命令说明。

## 构建与验证

在 Windows 上使用能够构建 SDK 风格 `net48` 项目的 .NET SDK，并使用 PowerShell 7 运行测试脚本。编译和模拟测试不需要 Lenovo DLL。

```powershell
.\build.ps1
.\tests\run-tests.ps1
.\verify-layout.ps1
```

便携版程序生成在 `artifacts\publish\EnergyControl.exe`。构建过程还会从三份 README 生成离线多语言帮助页面。

## 项目规则

参阅[贡献指南](CONTRIBUTING.md)、[安全策略](SECURITY.md)、[免责声明](DISCLAIMER.md)和[变更记录](CHANGELOG.md)。

本项目采用 [GPL-3.0-only](LICENSE) 许可证。Lenovo 仅用于说明兼容目标，本项目与联想无隶属关系，也未获得联想背书。
