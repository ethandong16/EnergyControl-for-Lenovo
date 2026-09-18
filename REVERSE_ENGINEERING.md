# 充电模式逆向记录

## 范围和结论

本记录针对本机安装的：

```text
PowerBattery.dll 1.0.13.79
SHA-256 D1EAB3B34699FE05E7E9A5AF2560FE7F576B4329394F868B7716085F7DB40D3F
```

静态反汇编和只读实测确认，`CChargingMode` 最终直接打开 `\\.\EnergyDrv`，并调用
`DeviceIoControl(0x831020F8)`。因此常规、养护和快充模式不需要 Vantage/百应进程，
但仍依赖 Lenovo ACPIVPC 驱动和受支持的 EC 固件。

自定义起充/停充百分比是另一条 Power RPC 路径。本机没有对应的
`BaseModuleRpcEndpoint` 服务端，初始化返回 `1722`；当前样本中也没有发现把两个
百分比直接传给 EnergyDrv 的命令。不能把固定养护模式宣称为任意百分比阈值。

## EnergyDrv 协议

设备打开参数与 Lenovo 原实现一致：读写访问、读写共享、`OPEN_EXISTING`。所有已知
命令均使用 1 字节输入和 4 字节输出。

```text
设备       \\.\EnergyDrv
控制码     0x831020F8
查询输入   FF
查询输出   uint32 flags
```

| 功能 | 命令或标志 | 证据 |
| --- | ---: | --- |
| 查询状态 | 输入 `FF` | 多个 getter 共用 |
| 养护已开启 | `flags & 0x20` | Storage 状态 getter |
| 开启普通养护 | 输入 `03` | Storage OpenFeature |
| 关闭普通养护 | 输入 `05` | Storage CloseFeature |
| 固定 80% 能力 | `flags & 0x4000` | `DoesSupportStorage80` |
| 开启固定 80% 扩展 | 输入 `0D` | 普通开启成功后、有能力位时追加 |
| 关闭固定 80% 扩展 | 输入 `0F` | 普通关闭成功后、有能力位时追加 |
| 快充已开启 | `flags & 0x04` | Rapid 状态 getter |
| 快充能力 | `flags & 0x20000` | Rapid capability getter |
| 开启快充 | 输入 `07` | Rapid OpenFeature |
| 关闭快充 | 输入 `08` | Rapid CloseFeature |

Lenovo 的 `SetChargingMode` 会在设置后最多查询 10 次，每次间隔 50 ms。直接后端沿用
相同验证方式，并确保养护和快充不会同时保持开启。

## 本机只读结果

```text
flags              0x00860022
mode               Storage
storage enabled    true
quick enabled      false
quick capable      true
storage80 capable  false
```

这说明本机当前处于养护模式，但没有 `0x4000` 固定 80% 能力位。实际停充百分比由固件
策略决定，不能仅凭 `Storage` 名称推断为 80%。本次逆向没有向驱动发送写命令。

## 安全边界

- CLI 写操作必须显式添加 `--apply`。
- GUI 写操作必须经过确认对话框。
- 固定 80% 扩展命令只在能力位存在时发送。
- 当前实现不枚举未知命令，也不向固件盲发百分比或功能 ID。
- Power RPC 可用时仍可独立读取和设置固件明确报告支持的自定义阈值。
