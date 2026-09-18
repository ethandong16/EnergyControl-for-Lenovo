# EnergyControl for Lenovo 兼容性调查

EnergyControl for Lenovo `v0.1.0-preview.1` 是非官方工具。直接充电后端是稳定路径；
Addin、性能和自定义百分比阈值是实验路径。程序不会打包或分发 Lenovo 私有 DLL，
可选组件只从用户自己的系统运行时加载。

## 结论

不是所有联想电脑都使用同一组设置。`IdeaNotebookAddin` 的接口名称相近，但实际
能力由机型、主板固件、CPU 平台、Windows 版本、Vantage/商用 Vantage 版本和驱动
共同决定。充电和性能两个能力也可能只实现其中一个，因此不能把“当前电脑返回的
三种充电模式、四种性能模式”当成全系标准。

本项目当前 Addin（程序集版本 `1.0.13.79`）实测返回：

```text
Supported-BatteryChargeMode = Normal,Storage,Quick
Supported-ITSMode            = MMC_Auto,MMC_Cool,MMC_Performance,MMC_Geek
WorkingDriver                = dispatcher
ShowGeekAsCreator            = False
IsGeekOptionGrey             = False
ErrorCode                    = 0
```

这证明当前设备支持这些能力，不代表其他产品线也支持。

## 产品线差异

| 产品线 | 常见控制入口 | 常见差异 | 对本软件的影响 |
| --- | --- | --- | --- |
| Legion / 拯救者、LOQ | Lenovo Vantage | 常见安静/均衡/性能或极致性能；部分型号还有自定义、混合显卡等独立开关；充电模式随电池和固件变化 | 不能假设一定有极客模式，也不能把显卡/混合模式当成 `ITSMode` |
| Yoga / IdeaPad / 小新 | Vantage、Lenovo PC Manager 或区域版工具 | 常见智能散热、节能/安静、性能；养护充电和快速充电按机型、适配器和 BIOS 提供 | `MMC_Cool` 的展示名称可能是安静、冷却或节能；性能选项通常少于 Legion |
| ThinkPad | Commercial Vantage、BIOS、电源管理驱动 | 常见电池充电阈值、智能充电和 Windows 电源模式；不少机型不提供 ITS 的四档模式 | 可能只有充电能力，性能接口应显示不可用而不是显示假按钮 |
| ThinkBook | Vantage 或 Commercial Vantage | 同一系列不同代际可能使用不同驱动栈，能力接近 Yoga/IdeaPad 或 ThinkPad | 必须以 `Supported-*` 和错误码为准，不能只按产品名判断 |
| 台式机 / 一体机 | Vantage（若安装）或 BIOS | 没有电池，因此充电功能没有意义；性能控制也可能不存在 | 充电读取失败应作为局部不可用，性能仍可独立读取 |
| ARM Windows、Chromebook、Linux | 通常没有该 Windows Addin | Addin、驱动或 RPC 端点不存在 | 启动后给出不支持说明，不应崩溃或尝试写入 |

官方指南能直接确认这些差异：

- Legion Slim 5 的“System operation modes”列出 Performance、Balance、Quiet，
  并明确说明使用电池或低功率适配器时可能无法切换到 Performance。
- IdeaPad Gaming 3 同样说明按 `Fn+Q` 切换，但电池供电时 Performance 不可用。
- ThinkBook 14s Yoga 列出 Intelligent Cooling、Battery Saving、Extreme Performance，
  名称与 Legion 并不相同。
- ThinkPad X1 Carbon Gen 10 / X1 Yoga Gen 7 的 Intelligent Cooling 在 Windows 10
  使用电源滑块，在 Windows 11 使用系统电源模式；手动档是 Eco/Balanced/Performance，
  另外还有可用 `Fn+T` 开关的 Auto。
- Yoga Pro 9i 指南明确列出 Normal、Rapid charge、Conservation，还包括 Overnight
  charge optimization；说明充电能力也不止本 Demo 当前的三个 setter 值。

因此上表中的“常见”不是稳定 SDK 契约。即使属于同一系列，电池供电、适配器功率、
操作系统、固件和驱动也会改变可用性；最终只能由本机 Addin 响应和 setter 结果确认。

## 命名兼容性

不同语言、地区和代际可能使用下列等价名称：

- 充电：`Normal` / `Standard`、`Storage` / `Conservation` / `LongLife`、
  `Quick` / `Express` / `RapidCharge`。
- 性能：`ITS_Auto` / `MMC_Auto` / `Balanced`、`MMC_Cool` / `Quiet` /
  `Cool` / `EnergySaving`、`MMC_Performance` / `Performance` / `Extreme`，
  以及 `MMC_Geek` / `Geek` / `Creator`。

这些别名只用于识别和展示；写入仍使用 Lenovo Contract 中的枚举值，且必须先在
`Supported-*` 中找到匹配能力。未知能力会在“支持”一行以“其他”显示，但不会生成
可写按钮。

## 正式版兼容性策略

1. Addin 只作为运行时可选后端；程序目录、环境变量和系统安装目录都不会进入发布 ZIP。
   目录没有时再定位
   `%ProgramData%\Lenovo\Vantage\Addins\IdeaNotebookAddin` 下的最高版本，避免把
   安装位置写死在构建环境中。
2. GUI 对充电和性能分别读取。台式机无电池、缺失驱动或某个接口不存在时，另一项
   仍可用，并显示局部不可用状态；如果 Addin 只有 getter 没有 setter，则显示只读。
3. 支持逗号、分号、竖线、斜线分隔的能力值，以及大小写和常见产品命名别名。
4. `IsGeekOptionGrey` 会隐藏暂不可用的极客/创作模式；`ShowGeekAsCreator` 和
   `ShowBsmAsQuietBsm` 控制显示名称。
5. CLI 写入前校验能力，写入后检查 `ErrorCode`；缺少能力或设备拒绝时不会报告成功。
6. 新增 `diagnose`（`capabilities` 别名）命令，输出 Addin 版本、进程架构和关键
   方法是否存在，便于区分“非联想设备、组件未安装、位数不匹配、驱动不支持”。
7. Addin 响应缺少 `settingList`、`ToJson` 形态变化或返回非 JSON 时，读取路径不会
   因为解析异常而误判为支持。
8. 自定义充电阈值通过 Power RPC 独立读取；服务未运行、客户端缺失或固件不支持时，
   只将阈值卡片标记为不可用，不影响充电模式和性能模式。
9. 充电模式优先使用 `EnergyDrv` 直接后端；其协议与任意百分比阈值接口相互独立。
   未报告固定 80% 能力位时，界面只显示“固件预设”，不猜测实际停充百分比。

## 验证方式与边界

```powershell
.\bin\Release\net48\EnergyControl.exe diagnose
.\bin\Release\net48\EnergyControl.exe status
.\verify-layout.ps1
```

`verify-layout.ps1` 已覆盖默认、窄窗口、宽窗口、150% 和 200% DPI，并验证能力
别名及多种分隔符。当前实验功能仍依赖用户系统中的 Lenovo 私有 Addin，而不是公开稳定 SDK；
Addin、BIOS 或驱动升级后应重新运行 `diagnose`/`status`。在没有真实硬件的环境中，
只能验证加载、解析和降级逻辑，不能替代各产品线的硬件回归测试。

## 官方资料来源

以下页面均为 Lenovo 官方用户指南，核对日期为 2026-09-11：

- [Legion Slim 5：System operation modes](https://download.lenovo.com/pccbbs/pubs/legion_slim_5/html_en/EN/common_topics_2022-8_RTM_updated/intro_operation_mode_legion.html)
- [IdeaPad Gaming 3：Set performance mode](https://download.lenovo.com/pccbbs/pubs/ip_gaming3_7/html_en/EN/performance_mode.html)
- [ThinkBook 14s Yoga：Set performance mode](https://download.lenovo.com/pccbbs/pubs/thinkbook_14s_yoga/html_en/EN/performance_mode.html)
- [ThinkPad X1 Carbon Gen 10 / X1 Yoga Gen 7：Intelligent cooling](https://download.lenovo.com/pccbbs/pubs/x1_carbon_gen10_yoga_gen7/html/html_en/explore_intelligent_cooling.html)
- [Yoga Pro 9i：Stay energetic](https://download.lenovo.com/pccbbs/pubs/yoga_pro9_16imh9/user_guide/en/help-center-stay-energetic.html)
