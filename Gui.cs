using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LenovoSettingsCompat;

namespace LenovoSettingsGui
{
    internal sealed partial class MainForm : Form
    {
        private readonly LenovoAddinClient client = new LenovoAddinClient();
        private readonly FlowLayoutPanel chargeModes = new WrappingFlowPanel();
        private readonly FlowLayoutPanel performanceModes = new WrappingFlowPanel();
        private readonly FlowLayoutPanel keyboardBacklightModes = new WrappingFlowPanel();
        private readonly FlowLayoutPanel keyboardBacklightActions = new WrappingFlowPanel();
        private readonly Label chargeCurrent = new Label();
        private readonly Label chargeSupported = new Label();
        private readonly Label thresholdCurrent = new Label();
        private readonly Label thresholdSupported = new Label();
        private readonly FlowLayoutPanel thresholdControls = new WrappingFlowPanel();
        private readonly NumericUpDown thresholdStart = new NumericUpDown();
        private readonly NumericUpDown thresholdStop = new NumericUpDown();
        private readonly Button thresholdApply = new Button();
        private readonly Label performanceCurrent = new Label();
        private readonly Label performanceSupported = new Label();
        private readonly Label keyboardBacklightCurrent = new Label();
        private readonly Label keyboardBacklightSupported = new Label();
        private readonly CheckBox keyboardBacklightReserveButton = new CheckBox();
        private readonly CheckBox keyboardBacklightAutoDimButton = new CheckBox();
        private readonly Button keyboardBacklightDefaultButton = new Button();
        private readonly TabControl settingsTabs = new TabControl();
        private readonly TabPage diagnosticsPage = new TabPage();
        private readonly TextBox diagnosticsText = new TextBox();
        private DeviceState lastState;
        private readonly Label statusLabel = new Label();
        private readonly Button refreshButton = new Button();
        private readonly Button aboutButton = new Button();
        private readonly Button diagnosticsButton = new Button();
        private EventHandler initialRefreshHandler;
        private bool busy;
        private bool geekOptionGrey;
        private bool thresholdAvailable;

        private static readonly ModeItem[] AllChargeModes =
        {
            new ModeItem("常规充电", "Normal", ChargeMode.Normal,
                "Standard", "Regular"),
            new ModeItem("养护充电", "Storage", ChargeMode.Storage,
                "Conservation", "BatteryConservation", "LongLife"),
            new ModeItem("快充", "Quick", ChargeMode.Quick,
                "Express", "Rapid", "RapidCharge")
        };

        private static readonly ModeItem[] AllPerformanceModes =
        {
            new ModeItem("自动", "MMC_Auto", PerformanceMode.Auto,
                "ITS_Auto", "MMC_Balance", "Auto", "Balance", "Balanced", "Smart",
                "IntelligentCooling"),
            new ModeItem("安静 / 节能", "MMC_Cool", PerformanceMode.Cool,
                "MMC_Quiet", "Quiet", "Silent", "Cool", "Bsm_Quiet", "BatterySaving", "EnergySaving"),
            new ModeItem(
                "高性能",
                "MMC_Performance",
                PerformanceMode.Performance,
                "MMC_Extreme", "Performance", "Extreme", "Turbo"),
            new ModeItem("极客模式", "MMC_Geek", PerformanceMode.Geek,
                "Geek", "Creator", "Creative")
        };

        private static readonly ModeItem[] AllKeyboardBacklightModes =
        {
            new ModeItem("关闭", "Off", KeyboardBacklightLevel.Off),
            new ModeItem("一级亮度", "Level_1", KeyboardBacklightLevel.Level1, "OneLevel"),
            new ModeItem("二级亮度", "Level_2", KeyboardBacklightLevel.Level2, "TwoLevels"),
            new ModeItem("自动", "Auto", KeyboardBacklightLevel.Auto, "TwoLevelsAuto")
        };

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "EnergyControl for Lenovo";
            Icon = AppIcon.Create();
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = Color.White;
            ForeColor = Color.FromArgb(32, 35, 42);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(700, 560);
            DoubleBuffered = true;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (busy && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    SetStatus("操作进行中，请稍候", Color.FromArgb(100, 110, 125));
                }
            };
            MinimumSize = new Size(560, 540);
            BuildLayout();
            SetBusy(true, "正在读取...");
            ResumeLayout(true);
            initialRefreshHandler = async delegate
            {
                busy = false;
                await RefreshStateAsync();
            };
            Shown += initialRefreshHandler;
        }

        internal void DisableInitialRefresh()
        {
            if (initialRefreshHandler != null)
            {
                Shown -= initialRefreshHandler;
                initialRefreshHandler = null;
            }
        }

        private async Task RefreshStateAsync()
        {
            if (busy) return;
            SetBusy(true, "正在读取...");
            try
            {
                DeviceState state = await Task.Run(
                    () => client.ReadState());
                DisplayState(state);
                int unavailable = 0;
                if (!String.IsNullOrWhiteSpace(state.ChargeError)) unavailable++;
                if (!String.IsNullOrWhiteSpace(state.ThresholdError)) unavailable++;
                if (!String.IsNullOrWhiteSpace(state.PerformanceError)) unavailable++;
                if (!String.IsNullOrWhiteSpace(state.KeyboardBacklightError) || !state.KeyboardBacklightSupported) unavailable++;
                if (!String.IsNullOrWhiteSpace(state.ErrorCode) &&
                    state.ErrorCode != "0")
                    SetStatus(
                        "设备返回错误 " + state.ErrorCode,
                        Color.FromArgb(180, 50, 45));
                else if (unavailable == 4)
                    SetStatus("设备设置接口不可用", Color.FromArgb(180, 50, 45));
                else if (unavailable > 0)
                    SetStatus("部分设置不可用", Color.FromArgb(165, 105, 25));
                else
                    SetStatus("读取成功", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("读取设备状态失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private Task ApplyChargeAsync(ModeItem selected)
        {
            if (selected == null) return Task.CompletedTask;
            return ApplyChangeAsync("充电模式已更新", "设置充电模式失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!before.ChargeWritable || !String.IsNullOrWhiteSpace(before.ChargeError) ||
                    !ContainsMode(before.SupportedChargeModes, selected))
                    throw new InvalidOperationException("设备当前不支持该充电模式，请刷新后重试。");
                object response = client.SetCharge((ChargeMode)selected.Value);
                if (!AddinResponse.IsSuccess(response))
                    throw new InvalidOperationException(
                        "设备拒绝了充电模式设置（ErrorCode=" +
                        (AddinResponse.ErrorCode(response) ?? "未知") + "）。");
                DeviceState result = client.ReadState();
                if (!IsCurrent(selected, result.ChargeMode))
                    throw new InvalidOperationException(
                        "设备未接受该设置，当前模式仍为 " +
                        DisplayName(AllChargeModes, result.ChargeMode) + "。");
                return result;
            });
        }

        private Task ApplyPerformanceAsync(ModeItem selected)
        {
            if (selected == null) return Task.CompletedTask;
            return ApplyChangeAsync("性能模式已更新", "设置性能模式失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!before.PerformanceWritable || !String.IsNullOrWhiteSpace(before.PerformanceError) ||
                    !ContainsMode(before.SupportedPerformanceModes, selected) ||
                    (selected.ContractName == "MMC_Geek" &&
                        String.Equals(before.IsGeekOptionGrey, "True", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("设备当前不支持该性能模式，请刷新后重试。");
                object response = client.SetPerformance((PerformanceMode)selected.Value);
                if (!AddinResponse.IsSuccess(response))
                    throw new InvalidOperationException(
                        "设备拒绝了性能模式设置（ErrorCode=" +
                        (AddinResponse.ErrorCode(response) ?? "未知") + "）。");
                DeviceState result = client.ReadState();
                if (!IsCurrent(selected, result.PerformanceMode))
                    throw new InvalidOperationException(
                        "设备未接受该设置，当前模式仍为 " +
                        DisplayName(
                            AllPerformanceModes,
                            result.PerformanceMode) + "。");
                return result;
            });
        }

        private Task ApplyThresholdAsync(int startValue, int stopValue)
        {
            return ApplyChangeAsync("充电阈值已更新", "设置充电阈值失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.ThresholdError) ||
                    !before.ThresholdCapable || !before.ThresholdWritable)
                    throw new InvalidOperationException(
                        "设备当前不支持写入自定义充电阈值，请刷新后重试。");
                client.SetThreshold(startValue, stopValue);
                DeviceState result = client.ReadState();
                if (!String.IsNullOrWhiteSpace(result.ThresholdError) ||
                    !result.ThresholdEnabled ||
                    result.ThresholdStart != startValue ||
                    result.ThresholdStop != stopValue)
                    throw new InvalidOperationException(
                        "设备未启用或未接受请求的阈值；当前为 " +
                        result.ThresholdStart + "% / " +
                        result.ThresholdStop + "% 。");
                return result;
            });
        }

        private Task ApplyKeyboardBacklightAsync(ModeItem selected)
        {
            if (selected == null) return Task.CompletedTask;
            return ApplyChangeAsync("键盘背光已更新", "设置键盘背光失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.KeyboardBacklightError) ||
                    !before.KeyboardBacklightSupported ||
                    !before.KeyboardBacklightWritable ||
                    !SupportsKeyboardBacklightMode(before.KeyboardBacklightLevelCapability, selected))
                    throw new InvalidOperationException("设备当前不支持该键盘背光档位，请刷新后重试。");
                object response = client.SetKeyboardBacklight(
                    (KeyboardBacklightLevel)selected.Value);
                EnsureKeyboardResponseSuccess(response, "键盘背光");
                DeviceState result = client.ReadState();
                if (!IsCurrent(selected, result.KeyboardBacklightStatus))
                    throw new InvalidOperationException(
                        "设备未接受该设置，当前为 " +
                        KeyboardBacklightNames.DisplayName(result.KeyboardBacklightStatus) + "。");
                return result;
            });
        }

        private Task ApplyKeyboardBacklightReserveAsync(bool enabled)
        {
            return ApplyChangeAsync("键盘背光保留状态已更新", "设置键盘背光保留状态失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.KeyboardBacklightError) ||
                    !before.KeyboardBacklightSupported ||
                    !before.KeyboardBacklightReserveWritable)
                    throw new InvalidOperationException("设备当前不支持键盘背光保留状态。");
                object response = client.SetKeyboardBacklightReserve(enabled);
                EnsureKeyboardResponseSuccess(response, "键盘背光保留状态");
                DeviceState result = client.ReadState();
                if (!IsBooleanValue(result.KeyboardBacklightReserve, enabled))
                    throw new InvalidOperationException("设备未接受键盘背光保留状态设置。");
                return result;
            });
        }

        private Task ApplyKeyboardBacklightAutoDimAsync(bool enabled)
        {
            return ApplyChangeAsync("键盘背光自动调暗已更新", "设置键盘背光自动调暗失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.KeyboardBacklightError) ||
                    !before.KeyboardBacklightSupported ||
                    !before.KeyboardBacklightAutoDimWritable)
                    throw new InvalidOperationException("设备当前不支持键盘背光自动调暗。");
                object response = client.SetKeyboardBacklightAutoDim(enabled);
                EnsureKeyboardResponseSuccess(response, "键盘背光自动调暗");
                DeviceState result = client.ReadState();
                if (!IsBooleanValue(result.KeyboardBacklightAutoDimStatus, enabled))
                    throw new InvalidOperationException("设备未接受键盘背光自动调暗设置。");
                return result;
            });
        }

        private Task RestoreKeyboardBacklightDefaultAsync()
        {
            return ApplyChangeAsync("键盘背光已恢复默认", "恢复键盘背光默认设置失败", () =>
            {
                DeviceState before = client.ReadState();
                if (!before.KeyboardBacklightSupported || !before.KeyboardBacklightRestoreWritable ||
                    !String.IsNullOrWhiteSpace(before.KeyboardBacklightError))
                    throw new InvalidOperationException("设备当前不支持恢复键盘背光默认设置。");
                object response = client.RestoreKeyboardBacklightDefault();
                EnsureKeyboardResponseSuccess(response, "键盘背光恢复默认");
                return client.ReadState();
            });
        }

        private async Task ApplyChangeAsync(string success, string failure, Func<DeviceState> operation)
        {
            if (busy) return;
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(operation);
                DisplayState(state);
                SetStatus(success, Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                // A rejected write can still change part of the device state.
                try { DisplayState(await Task.Run(() => client.ReadState())); }
                catch { }
                ShowError(failure, ex);
            }
            finally { SetBusy(false, null); }
        }

        private void DisplayState(DeviceState state)
        {
            if (state == null) return;
            lastState = state;
            UpdateDiagnostics(state);
            geekOptionGrey = String.Equals(
                state.IsGeekOptionGrey, "True", StringComparison.OrdinalIgnoreCase);
            foreach (ModeItem item in AllPerformanceModes)
            {
                if (item.ContractName == "MMC_Geek")
                    item.DisplayName = String.Equals(
                        state.ShowGeekAsCreator,
                        "True",
                        StringComparison.OrdinalIgnoreCase)
                        ? "创作模式"
                        : "极客模式";
                if (item.ContractName == "MMC_Cool")
                    item.DisplayName = String.Equals(
                        state.ShowBsmAsQuietBsm,
                        "True",
                        StringComparison.OrdinalIgnoreCase)
                        ? "安静 / 冷却"
                        : "安静 / 节能";
            }

            if (String.IsNullOrWhiteSpace(state.ChargeError))
            {
                chargeCurrent.Text =
                    "当前：" + DisplayName(AllChargeModes, state.ChargeMode);
                    chargeSupported.Text =
                        "支持：" + DisplaySupported(
                            AllChargeModes,
                        state.SupportedChargeModes) +
                        (state.ChargeWritable ? "" : "（只读）") +
                        (String.IsNullOrWhiteSpace(state.ChargeBackend)
                            ? ""
                            : "  ·  " + state.ChargeBackend);
                FillModes(
                    chargeModes,
                    AllChargeModes,
                    state.SupportedChargeModes,
                    state.ChargeMode,
                    state.ChargeWritable);
            }
            else
            {
                chargeCurrent.Text = "当前：不可用";
                chargeSupported.Text = "说明：" + ShortMessage(state.ChargeError);
                ClearModes(chargeModes);
            }

            if (String.IsNullOrWhiteSpace(state.ThresholdError))
            {
                if (state.ThresholdCapable)
                {
                    thresholdCurrent.Text = state.ThresholdEnabled
                        ? "当前：低于 " + state.ThresholdStart + "% 开始，充到 " +
                            state.ThresholdStop + "% 停止"
                        : "当前：阈值未启用（设备返回 " + state.ThresholdStart +
                            "% / " + state.ThresholdStop + "%）";
                    thresholdSupported.Text = state.ThresholdWritable
                        ? "支持：可设置起充和停充百分比"
                        : "支持：只读";
                    thresholdStart.Value = ClampThreshold(state.ThresholdStart, 0, 100);
                    thresholdStop.Value = ClampThreshold(state.ThresholdStop, 1, 100);
                }
                else
                {
                    thresholdCurrent.Text = "当前：不支持自定义百分比";
                    thresholdSupported.Text = String.IsNullOrWhiteSpace(
                        state.ChargeLimitInfo)
                        ? "说明：固件未报告自定义阈值能力"
                        : "养护模式：" + state.ChargeLimitInfo;
                }
                thresholdAvailable = state.ThresholdCapable && state.ThresholdWritable;
                thresholdControls.Visible = thresholdAvailable;
                thresholdControls.Enabled = thresholdAvailable && !busy;
            }
            else
            {
                thresholdCurrent.Text = "当前：自定义百分比不可用";
                thresholdSupported.Text = String.IsNullOrWhiteSpace(
                    state.ChargeLimitInfo)
                    ? "说明：" + ShortMessage(state.ThresholdError)
                    : "养护模式：" + state.ChargeLimitInfo;
                thresholdAvailable = false;
                thresholdControls.Visible = false;
                thresholdControls.Enabled = false;
            }

            if (String.IsNullOrWhiteSpace(state.PerformanceError))
            {
                performanceCurrent.Text =
                    "当前：" + DisplayName(
                        AllPerformanceModes,
                        state.PerformanceMode);
                    performanceSupported.Text =
                        "支持：" + DisplaySupported(
                            AllPerformanceModes,
                        state.SupportedPerformanceModes) +
                        (state.PerformanceWritable ? "" : "（只读）");
                FillModes(
                    performanceModes,
                    AllPerformanceModes,
                    state.SupportedPerformanceModes,
                    state.PerformanceMode,
                    state.PerformanceWritable);
            }
            else
            {
                performanceCurrent.Text = "当前：不可用";
                performanceSupported.Text = "说明：" + ShortMessage(state.PerformanceError);
                ClearModes(performanceModes);
            }

            if (String.IsNullOrWhiteSpace(state.KeyboardBacklightError) &&
                state.KeyboardBacklightSupported)
            {
                keyboardBacklightCurrent.Text = "当前：" +
                    KeyboardBacklightNames.DisplayName(state.KeyboardBacklightStatus);
                keyboardBacklightSupported.Text =
                    "支持：" + DisplayKeyboardBacklightSupported(state.KeyboardBacklightLevelCapability) +
                    (state.KeyboardBacklightWritable ? "" : "（只读）");
                FillKeyboardBacklightModes(
                    keyboardBacklightModes,
                    state.KeyboardBacklightLevelCapability,
                    state.KeyboardBacklightStatus,
                    state.KeyboardBacklightWritable);
                keyboardBacklightReserveButton.Tag = state.KeyboardBacklightReserve;
                keyboardBacklightReserveButton.Checked = IsTrue(state.KeyboardBacklightReserve);
                keyboardBacklightReserveButton.Enabled = state.KeyboardBacklightReserveWritable && !busy;
                keyboardBacklightAutoDimButton.Tag = state.KeyboardBacklightAutoDimStatus;
                keyboardBacklightAutoDimButton.Checked = IsTrue(state.KeyboardBacklightAutoDimStatus);
                keyboardBacklightAutoDimButton.Text = state.KeyboardBacklightAutoDimWritable ? "自动调暗" : "自动调暗（不可用）";
                keyboardBacklightAutoDimButton.Enabled = state.KeyboardBacklightAutoDimWritable && !busy;
                keyboardBacklightDefaultButton.Enabled = state.KeyboardBacklightWritable && !busy;
            }
            else
            {
                keyboardBacklightCurrent.Text = "当前：不可用";
                keyboardBacklightSupported.Text = "说明：" + ShortMessage(
                    String.IsNullOrWhiteSpace(state.KeyboardBacklightError)
                        ? "设备未报告键盘背光能力。"
                        : state.KeyboardBacklightError);
                ClearModes(keyboardBacklightModes);
                keyboardBacklightReserveButton.Enabled = false;
                keyboardBacklightAutoDimButton.Enabled = false;
                keyboardBacklightDefaultButton.Enabled = false;
            }

            UpdateActionAvailability();
        }

        private static void ClearModes(FlowLayoutPanel panel)
        {
            while (panel.Controls.Count > 0)
            {
                Control control = panel.Controls[0];
                panel.Controls.RemoveAt(0);
                control.Dispose();
            }
        }

        private void FillModes(
            FlowLayoutPanel panel,
            ModeItem[] allModes,
            string supported,
            string current,
            bool writable)
        {
            panel.SuspendLayout();
            try
            {
                ClearModes(panel);
                if (!writable) return;
                foreach (ModeItem item in allModes)
                {
                    if (ContainsMode(supported, item) &&
                        !(item.ContractName == "MMC_Geek" && geekOptionGrey))
                    {
                        RadioButton button = new RadioButton
                        {
                            Tag = item, Appearance = Appearance.Button, AutoCheck = false,
                            Checked = IsCurrent(item, current), TextAlign = ContentAlignment.MiddleCenter
                        };
                        StyleButton(button, item.DisplayName, IsCurrent(item, current));
                        button.Anchor = AnchorStyles.Left;
                        button.Margin = new Padding(0, 0, 10, 8);
                        button.AccessibleName = item.DisplayName;
                        button.Click += async delegate
                        {
                            if (!busy && !button.Checked)
                            {
                                if (MessageBox.Show(
                                    this,
                                    "确认切换为“" + item.DisplayName + "”吗？",
                                    "确认设置",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question) != DialogResult.Yes)
                                    return;
                                if (ReferenceEquals(panel, chargeModes))
                                    await ApplyChargeAsync((ModeItem)button.Tag);
                                else
                                    await ApplyPerformanceAsync((ModeItem)button.Tag);
                            }
                        };
                        panel.Controls.Add(button);
                    }
                }
            }
            finally
            {
                panel.ResumeLayout(true);
            }
        }

        private void FillKeyboardBacklightModes(
            FlowLayoutPanel panel, string capability, string current, bool writable)
        {
            panel.SuspendLayout();
            try
            {
                ClearModes(panel);
                if (!writable) return;
                foreach (ModeItem item in AllKeyboardBacklightModes)
                {
                    if (!SupportsKeyboardBacklightMode(capability, item)) continue;
                    RadioButton button = new RadioButton
                    {
                        Tag = item, Appearance = Appearance.Button, AutoCheck = false,
                        Checked = IsCurrent(item, current), TextAlign = ContentAlignment.MiddleCenter
                    };
                    StyleButton(button, item.DisplayName, IsCurrent(item, current));
                    button.Anchor = AnchorStyles.Left;
                    button.Margin = new Padding(0, 0, 10, 8);
                    button.AccessibleName = item.DisplayName;
                    button.Click += async delegate
                    {
                        if (busy || button.Checked) return;
                        if (MessageBox.Show(
                            this,
                            "确认切换键盘背光为“" + item.DisplayName + "”吗？",
                            "确认设置",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes)
                            return;
                        await ApplyKeyboardBacklightAsync((ModeItem)button.Tag);
                    };
                    panel.Controls.Add(button);
                }
            }
            finally
            {
                panel.ResumeLayout(true);
            }
        }

        private static string DisplaySupported(
            ModeItem[] allModes,
            string supported)
        {
            var names = new List<string>();
            List<string> raw = CapabilityNames.Split(supported);
            foreach (ModeItem item in allModes)
                if (ContainsMode(supported, item))
                    names.Add(item.DisplayName);
            var unknown = new List<string>();
            foreach (string value in raw)
            {
                bool known = false;
                foreach (ModeItem item in allModes)
                    if (CapabilityNames.Matches(value, item.SupportedNames))
                        known = true;
                if (!known) unknown.Add(value);
            }
            if (unknown.Count > 0)
            {
                int shown = Math.Min(2, unknown.Count);
                names.Add("其他（" + String.Join("、", unknown.GetRange(0, shown)) +
                    (unknown.Count > shown ? " 等" : "") + "）");
            }
            return names.Count == 0 ? "未报告" : String.Join("、", names);
        }

        private static string DisplayKeyboardBacklightSupported(string capability)
        {
            var names = new List<string>();
            foreach (ModeItem item in AllKeyboardBacklightModes)
                if (SupportsKeyboardBacklightMode(capability, item)) names.Add(item.DisplayName);
            return names.Count == 0 ? "未报告" : String.Join("、", names);
        }

        private static bool SupportsKeyboardBacklightMode(string capability, ModeItem item)
        {
            if (item == null) return false;
            if (item.ContractName == "Off") return true;
            if (String.IsNullOrWhiteSpace(capability)) return false;
            if (item.ContractName == "Level_1")
                return capability.IndexOf("OneLevel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    capability.IndexOf("TwoLevels", StringComparison.OrdinalIgnoreCase) >= 0;
            if (item.ContractName == "Level_2")
                return capability.IndexOf("TwoLevels", StringComparison.OrdinalIgnoreCase) >= 0;
            return item.ContractName == "Auto" &&
                capability.IndexOf("Auto", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DisplayName(
            ModeItem[] allModes,
            string contractName)
        {
            foreach (ModeItem item in allModes)
                if (IsCurrent(item, contractName))
                    return item.DisplayName;
            return String.IsNullOrWhiteSpace(contractName)
                ? "未知"
                : contractName;
        }

        private static bool IsCurrent(ModeItem item, string current)
        {
            return CapabilityNames.Matches(current, item.SupportedNames);
        }

        private static bool ContainsMode(string supported, ModeItem item)
        {
            return item != null &&
                CapabilityNames.Matches(supported, item.SupportedNames);
        }

        private static bool IsTrue(object value)
        {
            return String.Equals(
                Convert.ToString(value), "True", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(Convert.ToString(value), "1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBooleanValue(string value, bool expected)
        {
            if (String.Equals(value, "True", StringComparison.OrdinalIgnoreCase) || value == "1")
                return expected;
            if (String.Equals(value, "False", StringComparison.OrdinalIgnoreCase) || value == "0")
                return !expected;
            return false;
        }

        private static void EnsureKeyboardResponseSuccess(object response, string feature)
        {
            if (response is bool && !(bool)response)
                throw new InvalidOperationException(feature + "设置被设备拒绝。");
            if (!KeyboardBacklightResponse.IsSuccess(response))
                throw new InvalidOperationException(feature + "设置被设备拒绝（ErrorCode=" +
                    KeyboardBacklightResponse.ErrorCode(response) + "）。");
        }

        private static string ShortMessage(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "未报告";
            return value;
        }

        private static decimal ClampThreshold(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                this,
                "EnergyControl for Lenovo\n\n" +
                "v0.1.0-preview.1 · GPL-3.0-only\n" +
                "Unofficial community utility for compatible Lenovo systems.\n\n" +
                "Direct charging is the stable path. Performance, keyboard backlight and custom percentage thresholds are experimental.\n" +
                "This application does not include Lenovo private components, does not send telemetry, and does not represent Lenovo.",
                "关于 EnergyControl",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowDiagnostics()
        {
            settingsTabs.SelectedTab = diagnosticsPage;
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            refreshButton.Enabled = !value;
            chargeModes.Enabled = !value && chargeModes.Controls.Count > 0;
            thresholdControls.Enabled = !value && thresholdAvailable;
            performanceModes.Enabled =
                !value && performanceModes.Controls.Count > 0;
            keyboardBacklightModes.Enabled =
                !value && keyboardBacklightModes.Controls.Count > 0;
            keyboardBacklightActions.Enabled = !value;
            UpdateActionAvailability();
            UseWaitCursor = value;
            if (message != null)
                SetStatus(message, Color.FromArgb(80, 85, 95));
        }

        private void UpdateActionAvailability()
        {
            bool readable = lastState != null && lastState.KeyboardBacklightSupported &&
                String.IsNullOrWhiteSpace(lastState.KeyboardBacklightError);
            keyboardBacklightReserveButton.Enabled = !busy && readable &&
                lastState.KeyboardBacklightReserveWritable &&
                (IsBooleanValue(lastState.KeyboardBacklightReserve, true) ||
                 IsBooleanValue(lastState.KeyboardBacklightReserve, false));
            keyboardBacklightAutoDimButton.Enabled = !busy && readable &&
                lastState.KeyboardBacklightAutoDimWritable &&
                (IsBooleanValue(lastState.KeyboardBacklightAutoDimStatus, true) ||
                 IsBooleanValue(lastState.KeyboardBacklightAutoDimStatus, false));
            keyboardBacklightDefaultButton.Enabled = !busy && readable &&
                lastState.KeyboardBacklightRestoreWritable;
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }

        private void ShowError(string title, Exception exception)
        {
            Exception cause = exception.GetBaseException();
            SetStatus("操作失败", Color.FromArgb(180, 50, 45));
            MessageBox.Show(
                this,
                cause.Message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    internal static class GuiApplication
    {
        internal static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
