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
        private readonly Button helpButton = new Button();
        private EventHandler initialRefreshHandler;
        private bool busy;
        private bool geekOptionGrey;
        private bool thresholdAvailable;

        private readonly ModeItem[] AllChargeModes =
        {
            new ModeItem(UiText.Get("常规充电"), "Normal", ChargeMode.Normal,
                "Standard", "Regular"),
            new ModeItem(UiText.Get("养护充电"), "Storage", ChargeMode.Storage,
                "Conservation", "BatteryConservation", "LongLife"),
            new ModeItem(UiText.Get("快充"), "Quick", ChargeMode.Quick,
                "Express", "Rapid", "RapidCharge")
        };

        private readonly ModeItem[] AllPerformanceModes =
        {
            new ModeItem(UiText.Get("自动"), "MMC_Auto", PerformanceMode.Auto,
                "ITS_Auto", "MMC_Balance", "Auto", "Balance", "Balanced", "Smart",
                "IntelligentCooling"),
            new ModeItem(UiText.Get("安静 / 节能"), "MMC_Cool", PerformanceMode.Cool,
                "MMC_Quiet", "Quiet", "Silent", "Cool", "Bsm_Quiet", "BatterySaving", "EnergySaving"),
            new ModeItem(
                UiText.Get("高性能"),
                "MMC_Performance",
                PerformanceMode.Performance,
                "MMC_Extreme", "Performance", "Extreme", "Turbo"),
            new ModeItem(UiText.Get("极客模式"), "MMC_Geek", PerformanceMode.Geek,
                "Geek", "Creator", "Creative")
        };

        private readonly ModeItem[] AllKeyboardBacklightModes =
        {
            new ModeItem(UiText.Get("关闭"), "Off", KeyboardBacklightLevel.Off),
            new ModeItem(UiText.Get("一级亮度"), "Level_1", KeyboardBacklightLevel.Level1, "OneLevel"),
            new ModeItem(UiText.Get("二级亮度"), "Level_2", KeyboardBacklightLevel.Level2, "TwoLevels"),
            new ModeItem(UiText.Get("自动"), "Auto", KeyboardBacklightLevel.Auto, "TwoLevelsAuto")
        };

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "EnergyControl for Lenovo";
            Icon = AppIcon.Create();
            Font = new Font(UiText.FontFamily, 10F);
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
                    SetStatus(UiText.Get("操作进行中，请稍候"), Color.FromArgb(100, 110, 125));
                }
            };
            MinimumSize = new Size(560, 540);
            BuildLayout();
            SetBusy(true, UiText.Get("正在读取..."));
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
            SetBusy(true, UiText.Get("正在读取..."));
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
                        UiText.Get("设备返回错误 ") + state.ErrorCode,
                        Color.FromArgb(180, 50, 45));
                else if (unavailable == 4)
                    SetStatus(UiText.Get("设备设置接口不可用"), Color.FromArgb(180, 50, 45));
                else if (unavailable > 0)
                    SetStatus(UiText.Get("部分设置不可用"), Color.FromArgb(165, 105, 25));
                else
                    SetStatus(UiText.Get("读取成功"), Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError(UiText.Get("读取设备状态失败"), ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private Task ApplyChargeAsync(ModeItem selected)
        {
            if (selected == null) return Task.CompletedTask;
            return ApplyChangeAsync(UiText.Get("充电模式已更新"), UiText.Get("设置充电模式失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!before.ChargeWritable || !String.IsNullOrWhiteSpace(before.ChargeError) ||
                    !ContainsMode(before.SupportedChargeModes, selected))
                    throw new InvalidOperationException(UiText.Get("设备当前不支持该充电模式，请刷新后重试。"));
                object response = client.SetCharge((ChargeMode)selected.Value);
                if (!AddinResponse.IsSuccess(response))
                    throw new InvalidOperationException(
                        UiText.Get("设备拒绝了充电模式设置（ErrorCode=") +
                        (AddinResponse.ErrorCode(response) ?? UiText.Get("未知")) + UiText.Get("）。"));
                DeviceState result = client.ReadState();
                if (!IsCurrent(selected, result.ChargeMode))
                    throw new InvalidOperationException(
                        UiText.Get("设备未接受该设置，当前模式仍为 ") +
                        DisplayName(AllChargeModes, result.ChargeMode) + UiText.Get("。"));
                return result;
            });
        }

        private Task ApplyPerformanceAsync(ModeItem selected)
        {
            if (selected == null) return Task.CompletedTask;
            return ApplyChangeAsync(UiText.Get("性能模式已更新"), UiText.Get("设置性能模式失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!before.PerformanceWritable || !String.IsNullOrWhiteSpace(before.PerformanceError) ||
                    !ContainsMode(before.SupportedPerformanceModes, selected) ||
                    (selected.ContractName == "MMC_Geek" &&
                        String.Equals(before.IsGeekOptionGrey, "True", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(UiText.Get("设备当前不支持该性能模式，请刷新后重试。"));
                object response = client.SetPerformance((PerformanceMode)selected.Value);
                if (!AddinResponse.IsSuccess(response))
                    throw new InvalidOperationException(
                        UiText.Get("设备拒绝了性能模式设置（ErrorCode=") +
                        (AddinResponse.ErrorCode(response) ?? UiText.Get("未知")) + UiText.Get("）。"));
                DeviceState result = client.ReadState();
                if (!IsCurrent(selected, result.PerformanceMode))
                    throw new InvalidOperationException(
                        UiText.Get("设备未接受该设置，当前模式仍为 ") +
                        DisplayName(
                            AllPerformanceModes,
                            result.PerformanceMode) + UiText.Get("。"));
                return result;
            });
        }

        private Task ApplyThresholdAsync(int startValue, int stopValue)
        {
            return ApplyChangeAsync(UiText.Get("充电阈值已更新"), UiText.Get("设置充电阈值失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.ThresholdError) ||
                    !before.ThresholdCapable || !before.ThresholdWritable)
                    throw new InvalidOperationException(
                        UiText.Get("设备当前不支持写入自定义充电阈值，请刷新后重试。"));
                client.SetThreshold(startValue, stopValue);
                DeviceState result = client.ReadState();
                if (!String.IsNullOrWhiteSpace(result.ThresholdError) ||
                    !result.ThresholdEnabled ||
                    result.ThresholdStart != startValue ||
                    result.ThresholdStop != stopValue)
                    throw new InvalidOperationException(
                        UiText.Get("设备未启用或未接受请求的阈值；当前为 ") +
                        result.ThresholdStart + "% / " +
                        result.ThresholdStop + UiText.Get("% 。"));
                return result;
            });
        }

        private Task ApplyKeyboardBacklightAsync(ModeItem selected)
        {
            if (selected == null) return Task.CompletedTask;
            return ApplyChangeAsync(UiText.Get("键盘背光已更新"), UiText.Get("设置键盘背光失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.KeyboardBacklightError) ||
                    !before.KeyboardBacklightSupported ||
                    !before.KeyboardBacklightWritable ||
                    !SupportsKeyboardBacklightMode(before.KeyboardBacklightLevelCapability, selected))
                    throw new InvalidOperationException(UiText.Get("设备当前不支持该键盘背光档位，请刷新后重试。"));
                object response = client.SetKeyboardBacklight(
                    (KeyboardBacklightLevel)selected.Value);
                EnsureKeyboardResponseSuccess(response, UiText.Get("键盘背光"));
                DeviceState result = client.ReadState();
                if (!IsCurrent(selected, result.KeyboardBacklightStatus))
                    throw new InvalidOperationException(
                        UiText.Get("设备未接受该设置，当前为 ") +
                        KeyboardBacklightNames.DisplayName(result.KeyboardBacklightStatus) + UiText.Get("。"));
                return result;
            });
        }

        private Task ApplyKeyboardBacklightReserveAsync(bool enabled)
        {
            return ApplyChangeAsync(UiText.Get("键盘背光保留状态已更新"), UiText.Get("设置键盘背光保留状态失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.KeyboardBacklightError) ||
                    !before.KeyboardBacklightSupported ||
                    !before.KeyboardBacklightReserveWritable)
                    throw new InvalidOperationException(UiText.Get("设备当前不支持键盘背光保留状态。"));
                object response = client.SetKeyboardBacklightReserve(enabled);
                EnsureKeyboardResponseSuccess(response, UiText.Get("键盘背光保留状态"));
                DeviceState result = client.ReadState();
                if (!IsBooleanValue(result.KeyboardBacklightReserve, enabled))
                    throw new InvalidOperationException(UiText.Get("设备未接受键盘背光保留状态设置。"));
                return result;
            });
        }

        private Task ApplyKeyboardBacklightAutoDimAsync(bool enabled)
        {
            return ApplyChangeAsync(UiText.Get("键盘背光自动调暗已更新"), UiText.Get("设置键盘背光自动调暗失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!String.IsNullOrWhiteSpace(before.KeyboardBacklightError) ||
                    !before.KeyboardBacklightSupported ||
                    !before.KeyboardBacklightAutoDimWritable)
                    throw new InvalidOperationException(UiText.Get("设备当前不支持键盘背光自动调暗。"));
                object response = client.SetKeyboardBacklightAutoDim(enabled);
                EnsureKeyboardResponseSuccess(response, UiText.Get("键盘背光自动调暗"));
                DeviceState result = client.ReadState();
                if (!IsBooleanValue(result.KeyboardBacklightAutoDimStatus, enabled))
                    throw new InvalidOperationException(UiText.Get("设备未接受键盘背光自动调暗设置。"));
                return result;
            });
        }

        private Task RestoreKeyboardBacklightDefaultAsync()
        {
            return ApplyChangeAsync(UiText.Get("键盘背光已恢复默认"), UiText.Get("恢复键盘背光默认设置失败"), () =>
            {
                DeviceState before = client.ReadState();
                if (!before.KeyboardBacklightSupported || !before.KeyboardBacklightRestoreWritable ||
                    !String.IsNullOrWhiteSpace(before.KeyboardBacklightError))
                    throw new InvalidOperationException(UiText.Get("设备当前不支持恢复键盘背光默认设置。"));
                object response = client.RestoreKeyboardBacklightDefault();
                EnsureKeyboardResponseSuccess(response, UiText.Get("键盘背光恢复默认"));
                return client.ReadState();
            });
        }

        private async Task ApplyChangeAsync(string success, string failure, Func<DeviceState> operation)
        {
            if (busy) return;
            SetBusy(true, UiText.Get("正在应用..."));
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
                        ? UiText.Get("创作模式")
                        : UiText.Get("极客模式");
                if (item.ContractName == "MMC_Cool")
                    item.DisplayName = String.Equals(
                        state.ShowBsmAsQuietBsm,
                        "True",
                        StringComparison.OrdinalIgnoreCase)
                        ? UiText.Get("安静 / 冷却")
                        : UiText.Get("安静 / 节能");
            }

            if (String.IsNullOrWhiteSpace(state.ChargeError))
            {
                chargeCurrent.Text =
                    UiText.Get("当前：") + DisplayName(AllChargeModes, state.ChargeMode);
                    chargeSupported.Text =
                        UiText.Get("支持：") + DisplaySupported(
                            AllChargeModes,
                        state.SupportedChargeModes) +
                        (state.ChargeWritable ? "" : UiText.Get("（只读）")) +
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
                chargeCurrent.Text = UiText.Get("当前：不可用");
                chargeSupported.Text = UiText.Get("说明：") + ShortMessage(state.ChargeError);
                ClearModes(chargeModes);
            }

            if (String.IsNullOrWhiteSpace(state.ThresholdError))
            {
                if (state.ThresholdCapable)
                {
                    thresholdCurrent.Text = state.ThresholdEnabled
                        ? UiText.Get("当前：低于 ") + state.ThresholdStart + UiText.Get("% 开始，充到 ") +
                            state.ThresholdStop + UiText.Get("% 停止")
                        : UiText.Get("当前：阈值未启用（设备返回 ") + state.ThresholdStart +
                            "% / " + state.ThresholdStop + UiText.Get("%）");
                    thresholdSupported.Text = state.ThresholdWritable
                        ? UiText.Get("支持：可设置起充和停充百分比")
                        : UiText.Get("支持：只读");
                    thresholdStart.Value = ClampThreshold(state.ThresholdStart, 0, 100);
                    thresholdStop.Value = ClampThreshold(state.ThresholdStop, 1, 100);
                }
                else
                {
                    thresholdCurrent.Text = UiText.Get("当前：不支持自定义百分比");
                    thresholdSupported.Text = String.IsNullOrWhiteSpace(
                        state.ChargeLimitInfo)
                        ? UiText.Get("说明：固件未报告自定义阈值能力")
                        : UiText.Get("养护模式：") + state.ChargeLimitInfo;
                }
                thresholdAvailable = state.ThresholdCapable && state.ThresholdWritable;
                thresholdControls.Visible = thresholdAvailable;
                thresholdControls.Enabled = thresholdAvailable && !busy;
            }
            else
            {
                thresholdCurrent.Text = UiText.Get("当前：自定义百分比不可用");
                thresholdSupported.Text = String.IsNullOrWhiteSpace(
                    state.ChargeLimitInfo)
                    ? UiText.Get("说明：") + ShortMessage(state.ThresholdError)
                    : UiText.Get("养护模式：") + state.ChargeLimitInfo;
                thresholdAvailable = false;
                thresholdControls.Visible = false;
                thresholdControls.Enabled = false;
            }

            if (String.IsNullOrWhiteSpace(state.PerformanceError))
            {
                performanceCurrent.Text =
                    UiText.Get("当前：") + DisplayName(
                        AllPerformanceModes,
                        state.PerformanceMode);
                    performanceSupported.Text =
                        UiText.Get("支持：") + DisplaySupported(
                            AllPerformanceModes,
                        state.SupportedPerformanceModes) +
                        (state.PerformanceWritable ? "" : UiText.Get("（只读）"));
                FillModes(
                    performanceModes,
                    AllPerformanceModes,
                    state.SupportedPerformanceModes,
                    state.PerformanceMode,
                    state.PerformanceWritable);
            }
            else
            {
                performanceCurrent.Text = UiText.Get("当前：不可用");
                performanceSupported.Text = UiText.Get("说明：") + ShortMessage(state.PerformanceError);
                ClearModes(performanceModes);
            }

            if (String.IsNullOrWhiteSpace(state.KeyboardBacklightError) &&
                state.KeyboardBacklightSupported)
            {
                keyboardBacklightCurrent.Text = UiText.Get("当前：") +
                    KeyboardBacklightNames.DisplayName(state.KeyboardBacklightStatus);
                keyboardBacklightSupported.Text =
                    UiText.Get("支持：") + DisplayKeyboardBacklightSupported(state.KeyboardBacklightLevelCapability) +
                    (state.KeyboardBacklightWritable ? "" : UiText.Get("（只读）"));
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
                keyboardBacklightAutoDimButton.Text = state.KeyboardBacklightAutoDimWritable ? UiText.Get("自动调暗") : UiText.Get("自动调暗（不可用）");
                keyboardBacklightAutoDimButton.Enabled = state.KeyboardBacklightAutoDimWritable && !busy;
                keyboardBacklightDefaultButton.Enabled = state.KeyboardBacklightWritable && !busy;
            }
            else
            {
                keyboardBacklightCurrent.Text = UiText.Get("当前：不可用");
                keyboardBacklightSupported.Text = UiText.Get("说明：") + ShortMessage(
                    String.IsNullOrWhiteSpace(state.KeyboardBacklightError)
                        ? UiText.Get("设备未报告键盘背光能力。")
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
                                    UiText.Get("确认切换为“") + item.DisplayName + UiText.Get("”吗？"),
                                    UiText.Get("确认设置"),
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
                            UiText.Get("确认切换键盘背光为“") + item.DisplayName + UiText.Get("”吗？"),
                            UiText.Get("确认设置"),
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
                names.Add(UiText.Get("其他（") + String.Join(UiText.Get("、"), unknown.GetRange(0, shown)) +
                    (unknown.Count > shown ? UiText.Get(" 等") : "") + UiText.Get("）"));
            }
            return names.Count == 0 ? UiText.Get("未报告") : String.Join(UiText.Get("、"), names);
        }

        private string DisplayKeyboardBacklightSupported(string capability)
        {
            var names = new List<string>();
            foreach (ModeItem item in AllKeyboardBacklightModes)
                if (SupportsKeyboardBacklightMode(capability, item)) names.Add(item.DisplayName);
            return names.Count == 0 ? UiText.Get("未报告") : String.Join(UiText.Get("、"), names);
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
                ? UiText.Get("未知")
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
                throw new InvalidOperationException(feature + UiText.Get("设置被设备拒绝。"));
            if (!KeyboardBacklightResponse.IsSuccess(response))
                throw new InvalidOperationException(feature + UiText.Get("设置被设备拒绝（ErrorCode=") +
                    KeyboardBacklightResponse.ErrorCode(response) + UiText.Get("）。"));
        }

        private static string ShortMessage(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return UiText.Get("未报告");
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
                UiText.Get("EnergyControl for Lenovo\n\nv0.1.0-preview.1 | GPL-3.0-only\n\n适用于兼容 Lenovo 设备的非官方工具。\n性能、背光和自定义充电阈值为实验功能。\n不包含 Lenovo 私有组件或遥测。"),
                UiText.Get("关于 EnergyControl"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowDiagnostics()
        {
            settingsTabs.SelectedTab = diagnosticsPage;
        }

        private void ShowHelp()
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "README.html");
                string target = System.IO.File.Exists(path)
                    ? new Uri(path).AbsoluteUri + "?lang=" + UiText.Language
                    : "https://github.com/ethandong16/EnergyControl-for-Lenovo/blob/main/" + UiText.ReadmeFile;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { ShowError(UiText.Get("无法打开帮助"), ex); }
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
            SetStatus(UiText.Get("操作失败"), Color.FromArgb(180, 50, 45));
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
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture =
                System.Globalization.CultureInfo.CurrentUICulture;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
