using System;
using System.Collections.Generic;
using System.Globalization;

namespace LenovoSettingsCompat
{
    internal static class UiText
    {
        // Source text remains the Chinese translation; unsupported languages use English.
        private static readonly Dictionary<string, string[]> Translations =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "常规充电", new[] { "Normal", "通常充電" } },
                { "养护充电", new[] { "Conservation", "バッテリー保護" } },
                { "快充", new[] { "Express", "急速充電" } },
                { "自动", new[] { "Auto", "自動" } },
                { "安静 / 节能", new[] { "Quiet / Power saving", "静音 / 省電力" } },
                { "高性能", new[] { "Performance", "高性能" } },
                { "极客模式", new[] { "Geek mode", "エキスパート" } },
                { "关闭", new[] { "Off", "オフ" } },
                { "一级亮度", new[] { "Level 1", "明るさ 1" } },
                { "二级亮度", new[] { "Level 2", "明るさ 2" } },
                { "操作进行中，请稍候", new[] { "Operation in progress. Please wait.", "処理中です。しばらくお待ちください。" } },
                { "正在读取...", new[] { "Reading...", "読み取り中..." } },
                { "设备返回错误 ", new[] { "Device error: ", "デバイスエラー: " } },
                { "设备设置接口不可用", new[] { "Device settings unavailable", "デバイス設定を利用できません" } },
                { "部分设置不可用", new[] { "Some settings unavailable", "一部の設定を利用できません" } },
                { "读取成功", new[] { "Updated", "更新しました" } },
                { "读取设备状态失败", new[] { "Could not read device status", "デバイス状態の読み取りに失敗しました" } },
                { "充电模式已更新", new[] { "Charging mode updated", "充電モードを更新しました" } },
                { "设置充电模式失败", new[] { "Could not set charging mode", "充電モードを変更できませんでした" } },
                { "设备当前不支持该充电模式，请刷新后重试。", new[] { "This charging mode is unavailable. Refresh and try again.", "この充電モードは利用できません。更新して再試行してください。" } },
                { "设备拒绝了充电模式设置（ErrorCode=", new[] { "Charging mode rejected (ErrorCode=", "充電モードが拒否されました（ErrorCode=" } },
                { "未知", new[] { "Unknown", "不明" } },
                { "设备未接受该设置，当前模式仍为 ", new[] { "Change not accepted. Current mode: ", "変更が反映されませんでした。現在のモード: " } },
                { "性能模式已更新", new[] { "Performance mode updated", "パフォーマンスモードを更新しました" } },
                { "设置性能模式失败", new[] { "Could not set performance mode", "パフォーマンスモードを変更できませんでした" } },
                { "设备当前不支持该性能模式，请刷新后重试。", new[] { "This performance mode is unavailable. Refresh and try again.", "このパフォーマンスモードは利用できません。更新して再試行してください。" } },
                { "设备拒绝了性能模式设置（ErrorCode=", new[] { "Performance mode rejected (ErrorCode=", "パフォーマンスモードが拒否されました（ErrorCode=" } },
                { "充电阈值已更新", new[] { "Charge thresholds updated", "充電しきい値を更新しました" } },
                { "设置充电阈值失败", new[] { "Could not set charge thresholds", "充電しきい値を変更できませんでした" } },
                { "设备当前不支持写入自定义充电阈值，请刷新后重试。", new[] { "Custom charge thresholds are unavailable. Refresh and try again.", "充電しきい値の変更は利用できません。更新して再試行してください。" } },
                { "设备未启用或未接受请求的阈值；当前为 ", new[] { "Thresholds were not enabled or accepted. Current values: ", "しきい値が有効にならないか、変更が拒否されました。現在の値: " } },
                { "键盘背光已更新", new[] { "Keyboard backlight updated", "キーボードのバックライトを更新しました" } },
                { "设置键盘背光失败", new[] { "Could not set keyboard backlight", "バックライトを変更できませんでした" } },
                { "设备当前不支持该键盘背光档位，请刷新后重试。", new[] { "This backlight level is unavailable. Refresh and try again.", "この明るさは利用できません。更新して再試行してください。" } },
                { "键盘背光", new[] { "Keyboard backlight", "キーボードのバックライト" } },
                { "设备未接受该设置，当前为 ", new[] { "Change not accepted. Current setting: ", "変更が反映されませんでした。現在の設定: " } },
                { "键盘背光保留状态已更新", new[] { "Backlight memory updated", "バックライトの記憶設定を更新しました" } },
                { "设置键盘背光保留状态失败", new[] { "Could not set backlight memory", "バックライトの記憶設定を変更できませんでした" } },
                { "设备当前不支持键盘背光保留状态。", new[] { "Backlight memory is unavailable.", "バックライト状態の記憶は利用できません。" } },
                { "键盘背光保留状态", new[] { "Backlight memory", "バックライト状態の記憶" } },
                { "设备未接受键盘背光保留状态设置。", new[] { "Backlight memory change was not accepted.", "バックライトの記憶設定が反映されませんでした。" } },
                { "键盘背光自动调暗已更新", new[] { "Backlight auto-dim updated", "自動減光を更新しました" } },
                { "设置键盘背光自动调暗失败", new[] { "Could not set backlight auto-dim", "自動減光を変更できませんでした" } },
                { "设备当前不支持键盘背光自动调暗。", new[] { "Backlight auto-dim is unavailable.", "バックライトの自動減光は利用できません。" } },
                { "键盘背光自动调暗", new[] { "Backlight auto-dim", "バックライトの自動減光" } },
                { "设备未接受键盘背光自动调暗设置。", new[] { "Backlight auto-dim change was not accepted.", "自動減光の変更が反映されませんでした。" } },
                { "键盘背光已恢复默认", new[] { "Backlight defaults requested", "バックライトの初期設定を適用しました" } },
                { "恢复键盘背光默认设置失败", new[] { "Could not restore backlight defaults", "バックライトを初期設定に戻せませんでした" } },
                { "设备当前不支持恢复键盘背光默认设置。", new[] { "Restoring backlight defaults is unavailable.", "バックライトの初期設定への復元は利用できません。" } },
                { "键盘背光恢复默认", new[] { "Backlight defaults", "バックライトの初期設定" } },
                { "正在应用...", new[] { "Applying...", "適用中..." } },
                { "创作模式", new[] { "Creator", "クリエイター" } },
                { "安静 / 冷却", new[] { "Quiet / Cooling", "静音 / 冷却" } },
                { "当前：", new[] { "Current: ", "現在: " } },
                { "支持：", new[] { "Available: ", "利用可能: " } },
                { "（只读）", new[] { " (read-only)", "（読み取り専用）" } },
                { "当前：不可用", new[] { "Current: unavailable", "現在: 利用不可" } },
                { "说明：", new[] { "Details: ", "詳細: " } },
                { "当前：低于 ", new[] { "Current: start below ", "現在: 充電開始 " } },
                { "% 开始，充到 ", new[] { "%, stop at ", "% 未満、充電停止 " } },
                { "% 停止", new[] { "%", "%" } },
                { "当前：阈值未启用（设备返回 ", new[] { "Current: disabled (device reports ", "現在: 無効（デバイスの値: " } },
                { "支持：可设置起充和停充百分比", new[] { "Available: start and stop percentages", "利用可能: 充電開始・停止の割合" } },
                { "支持：只读", new[] { "Available: read-only", "利用可能: 読み取り専用" } },
                { "当前：不支持自定义百分比", new[] { "Custom percentages not supported", "任意の割合には対応していません" } },
                { "说明：固件未报告自定义阈值能力", new[] { "Firmware does not report threshold support.", "ファームウェアが充電しきい値に対応していません。" } },
                { "养护模式：", new[] { "Conservation: ", "バッテリー保護: " } },
                { "当前：自定义百分比不可用", new[] { "Custom percentages unavailable", "任意の割合は利用できません" } },
                { "自动调暗", new[] { "Auto-dim", "自動減光" } },
                { "自动调暗（不可用）", new[] { "Auto-dim (unavailable)", "自動減光（利用不可）" } },
                { "设备未报告键盘背光能力。", new[] { "Keyboard backlight support was not reported.", "キーボードのバックライトに対応していません。" } },
                { "确认切换为“", new[] { "Switch to ", "「" } },
                { "”吗？", new[] { "?", "」に変更しますか？" } },
                { "确认设置", new[] { "Confirm change", "変更の確認" } },
                { "确认切换键盘背光为“", new[] { "Set keyboard backlight to ", "バックライトを「" } },
                { "其他（", new[] { "Other (", "その他（" } },
                { " 等", new[] { " and more", " など" } },
                { "未报告", new[] { "Not reported", "情報なし" } },
                { "设置被设备拒绝。", new[] { " was rejected by the device.", "の設定がデバイスに拒否されました。" } },
                { "设置被设备拒绝（ErrorCode=", new[] { " was rejected (ErrorCode=", "の設定が拒否されました（ErrorCode=" } },
                { "关于 EnergyControl", new[] { "About EnergyControl", "EnergyControl について" } },
                { "操作失败", new[] { "Operation failed", "操作に失敗しました" } },
                { "刷新", new[] { "Refresh", "更新" } },
                { "诊断", new[] { "Diagnostics", "診断" } },
                { "关于", new[] { "About", "情報" } },
                { "设备设置", new[] { "Device settings", "デバイス設定" } },
                { "电池", new[] { "Battery", "バッテリー" } },
                { "充电模式", new[] { "Charging mode", "充電モード" } },
                { "性能", new[] { "Performance", "パフォーマンス" } },
                { "性能模式", new[] { "Performance mode", "パフォーマンスモード" } },
                { "键盘", new[] { "Keyboard", "キーボード" } },
                { "设备诊断报告", new[] { "Device diagnostic report", "デバイス診断レポート" } },
                { "尚未读取设备状态", new[] { "Device status has not been read yet.", "デバイスの状態はまだ読み取られていません。" } },
                { "当前：读取中…", new[] { "Current: reading...", "現在: 読み取り中..." } },
                { "支持：读取中…", new[] { "Available: reading...", "利用可能: 読み取り中..." } },
                { "键盘背光亮度选项", new[] { "Keyboard backlight levels", "バックライトの明るさ" } },
                { "记住背光状态", new[] { "Remember backlight", "バックライト状態を記憶" } },
                { "恢复默认", new[] { "Restore defaults", "初期設定に戻す" } },
                { "确认切换键盘背光保留状态吗？", new[] { "Toggle backlight memory?", "バックライト状態の記憶を切り替えますか？" } },
                { "确认切换键盘背光自动调暗吗？", new[] { "Toggle backlight auto-dim?", "バックライトの自動減光を切り替えますか？" } },
                { "确认恢复键盘背光默认设置吗？", new[] { "Restore default keyboard backlight settings?", "バックライトを初期設定に戻しますか？" } },
                { "充电阈值", new[] { "Charge thresholds", "充電しきい値" } },
                { "应用阈值", new[] { "Apply thresholds", "しきい値を適用" } },
                { "阈值无效", new[] { "Invalid thresholds", "しきい値が無効です" } },
                { "确认设置为低于 ", new[] { "Start charging below ", "次の値で充電を設定しますか。開始: " } },
                { "% 开始充电，充到 ", new[] { "% and stop at ", "% 未満、停止: " } },
                { "% 停止吗？", new[] { "%?", "%？" } },
                { "自定义充电阈值设置", new[] { "Custom charge thresholds", "充電しきい値の設定" } },
                { "低于", new[] { "Start below", "開始" } },
                { "% 开始，充到", new[] { "%, stop at", "% 未満、停止" } },
                { "选项", new[] { " options", "の選択肢" } },
                { "读取时间：", new[] { "Last read: ", "最終読み取り: " } },
                { "充电模式：", new[] { "Charging mode: ", "充電モード: " } },
                { "充电后端：", new[] { "Charging backend: ", "充電バックエンド: " } },
                { "不可用", new[] { "Unavailable", "利用不可" } },
                { "充电错误：", new[] { "Charging error: ", "充電エラー: " } },
                { "无", new[] { "None", "なし" } },
                { "阈值能力：", new[] { "Threshold support: ", "しきい値への対応: " } },
                { "阈值错误：", new[] { "Threshold error: ", "しきい値エラー: " } },
                { "性能模式：", new[] { "Performance mode: ", "パフォーマンスモード: " } },
                { "性能错误：", new[] { "Performance error: ", "パフォーマンスエラー: " } },
                { "性能驱动：", new[] { "Performance driver: ", "パフォーマンスドライバー: " } },
                { "背光状态：", new[] { "Backlight status: ", "バックライト状態: " } },
                { "背光档位：", new[] { "Backlight levels: ", "バックライトの明るさ: " } },
                { "保留状态：", new[] { "Remember state: ", "状態の記憶: " } },
                { "自动调暗能力：", new[] { "Auto-dim support: ", "自動減光への対応: " } },
                { "自动调暗状态：", new[] { "Auto-dim status: ", "自動減光の状態: " } },
                { "背光错误：", new[] { "Backlight error: ", "バックライトエラー: " } },
                { "未找到", new[] { "Not found", "見つかりません" } },
                { "直接驱动", new[] { "Direct driver", "ドライバー直接制御" } },
                { "直接驱动：", new[] { "Direct driver: ", "ドライバー直接制御: " } },
                { "关闭（禁用）", new[] { "Off (disabled)", "オフ（無効）" } },
                { "无法构造 KeyboardSettingsRequest 请求体。", new[] { "Could not construct KeyboardSettingsRequest.", "KeyboardSettingsRequest を構築できません。" } },
                { "未检测到 Lenovo Vantage/百应的 IdeaNotebookAddin。此电脑可能不是联想设备，或相关服务未安装。", new[] { "Lenovo IdeaNotebookAddin was not found. Check your device and installed Vantage components.", "Lenovo IdeaNotebookAddin が見つかりません。機種と Vantage コンポーネントを確認してください。" } },
                { "未找到兼容的 Lenovo 设备代理类型。", new[] { "No compatible Lenovo device agent was found.", "互換性のある Lenovo デバイスエージェントが見つかりません。" } },
                { "未在 Addin 中找到兼容的设备代理类型。可能是商用 Vantage 或不匹配的版本。", new[] { "No compatible agent in this Addin. Its version or Vantage edition may be unsupported.", "この Addin に互換性のあるエージェントがありません。バージョンまたは Vantage の種類が非対応の可能性があります。" } },
                { "未知（WMI 不可用）", new[] { "Unknown (WMI unavailable)", "不明（WMI 利用不可）" } },
                { "Lenovo Power RPC 服务未运行，请先启动 Lenovo Vantage 或联想百应", new[] { "Lenovo Power RPC is unavailable. Start Lenovo Vantage or Baiying.", "Lenovo Power RPC を利用できません。Lenovo Vantage または Baiying を起動してください。" } },
                { "Lenovo Power RPC 会话不可用", new[] { "Lenovo Power RPC session unavailable", "Lenovo Power RPC セッションを利用できません" } },
                { "设备返回错误", new[] { "Device error", "デバイスエラー" } },
                { "失败：", new[] { " failed: ", "に失敗: " } },
                { "（错误码 ", new[] { " (error code ", "（エラーコード " } },
                { "读取充电阈值", new[] { "Read charge thresholds", "充電しきい値の読み取り" } },
                { "设置充电阈值", new[] { "Set charge thresholds", "充電しきい値の設定" } },
                { "起充阈值必须在 0 到 100 之间。", new[] { "Start percentage must be between 0 and 100.", "開始の割合は 0～100 にしてください。" } },
                { "停充阈值必须在 1 到 100 之间。", new[] { "Stop percentage must be between 1 and 100.", "停止の割合は 1～100 にしてください。" } },
                { "起充阈值必须小于停充阈值。", new[] { "Start percentage must be lower than stop percentage.", "開始の割合は停止の割合より小さくしてください。" } },
                { "未检测到 Lenovo Power RPC 客户端，无法读取自定义充电阈值。", new[] { "Lenovo Power RPC client not found; charge thresholds cannot be read.", "Lenovo Power RPC クライアントが見つからないため、充電しきい値を読み取れません。" } },
                { "Lenovo Power RPC 客户端中不存在 ", new[] { "Lenovo Power RPC client does not provide ", "Lenovo Power RPC クライアントに次の型がありません: " } },
                { "无法创建 Lenovo Power RPC 客户端。", new[] { "Could not create Lenovo Power RPC client.", "Lenovo Power RPC クライアントを作成できません。" } },
                { "初始化充电阈值接口", new[] { "Initialize charge thresholds", "充電しきい値の初期化" } },
                { "电池槽位不能为负数。", new[] { "Battery slot cannot be negative.", "バッテリースロットは 0 以上にしてください。" } },
                { "固件固定 80%", new[] { "Firmware-fixed 80%", "ファームウェア固定 80%" } },
                { "固件预设（未报告固定 80% 能力）", new[] { "Firmware preset (fixed 80% not reported)", "ファームウェア既定値（固定 80% の対応情報なし）" } },
                { "固件未报告快充能力。", new[] { "Firmware does not report express charging support.", "ファームウェアが急速充電に対応していません。" } },
                { "驱动接受了请求，但充电模式没有切换到 ", new[] { "Driver accepted the request but the mode did not change to ", "要求は受理されましたが、充電モードを変更できません。指定値: " } },
                { "；当前为 ", new[] { "; current: ", "、現在値: " } },
                { "。不同固件可能需要重新插拔电源后生效。", new[] { ". Some firmware may require reconnecting power.", "。ファームウェアによっては電源の再接続が必要です。" } },
                { "拒绝未列入白名单的 EnergyDrv 命令。", new[] { "Blocked an unrecognized EnergyDrv command.", "未登録の EnergyDrv コマンドを拒否しました。" } },
                { "无法打开 Lenovo EnergyDrv 设备；请确认 ACPIVPC 驱动已安装", new[] { "Cannot open Lenovo EnergyDrv. Check the ACPIVPC driver installation.", "Lenovo EnergyDrv を開けません。ACPIVPC ドライバーを確認してください。" } },
                { "EnergyDrv 命令 0x", new[] { "EnergyDrv command 0x", "EnergyDrv コマンド 0x" } },
                { " 失败", new[] { " failed", " が失敗しました" } },
                { "EnergyDrv 返回了意外的数据长度：", new[] { "Unexpected EnergyDrv response length: ", "EnergyDrv の応答長が不正です: " } },
                { "。", new[] { ".", "。" } },
                { "）。", new[] { ").", "）。" } },
                { "）", new[] { ")", "）" } },
                { "%）", new[] { "%)", "%）" } },
                { "% 。", new[] { "%.", "%。" } },
                { "；Addin：", new[] { "; Addin: ", "、Addin: " } },
                { "、", new[] { ", ", "、" } },
                { "Lenovo Addin：", new[] { "Lenovo Addin: ", "Lenovo Addin: " } },
                { "帮助", new[] { "Help", "ヘルプ" } },
                { "无法打开帮助", new[] { "Could not open help", "ヘルプを開けませんでした" } },
                { "是", new[] { "Yes", "はい" } },
                { "否", new[] { "No", "いいえ" } },
                { "EnergyControl for Lenovo\n\nv0.1.0-preview.1 | GPL-3.0-only\n\n适用于兼容 Lenovo 设备的非官方工具。\n性能、背光和自定义充电阈值为实验功能。\n不包含 Lenovo 私有组件或遥测。", new[] { "EnergyControl for Lenovo\n\nv0.1.0-preview.1 | GPL-3.0-only\n\nUnofficial utility for compatible Lenovo devices.\nPerformance, backlight and custom thresholds are experimental.\nNo Lenovo private components or telemetry.", "EnergyControl for Lenovo\n\nv0.1.0-preview.1 | GPL-3.0-only\n\n互換性のある Lenovo 機種向けの非公式ツールです。\nパフォーマンス、バックライト、充電しきい値は実験的機能です。\nLenovo の非公開コンポーネントやテレメトリは含みません。" } },
            };

        public static string ResolveLanguage(string cultureName)
        {
            if (String.IsNullOrWhiteSpace(cultureName)) return "en";
            try
            {
                string language = CultureInfo.GetCultureInfo(cultureName).TwoLetterISOLanguageName;
                return language == "zh" || language == "ja" ? language : "en";
            }
            catch (CultureNotFoundException) { return "en"; }
        }

        public static string Language
        {
            get { return ResolveLanguage(CultureInfo.CurrentUICulture.Name); }
        }

        public static string FontFamily
        {
            get { return Language == "ja" ? "Yu Gothic UI" :
                Language == "zh" ? "Microsoft YaHei UI" : "Segoe UI"; }
        }

        public static string ReadmeFile
        {
            get { return Language == "zh" ? "README.zh-CN.md" :
                Language == "ja" ? "README.ja.md" : "README.md"; }
        }

        public static string Get(string source)
        {
            if (source == null) return null;
            string[] translated;
            if (Language == "zh" || !Translations.TryGetValue(source, out translated)) return source;
            return translated[Language == "ja" ? 1 : 0];
        }
    }
}
