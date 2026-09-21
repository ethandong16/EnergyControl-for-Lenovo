using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LenovoSettingsCompat;

namespace LenovoSettingsGui
{
    internal sealed class ModeItem
    {
        public string DisplayName { get; set; }
        public string ContractName { get; private set; }
        public object Value { get; private set; }
        public string[] SupportedNames { get; private set; }

        public ModeItem(string displayName, string contractName, object value,
            params string[] aliases)
        {
            DisplayName = displayName;
            ContractName = contractName;
            Value = value;
            var names = new List<string> { contractName };
            if (aliases != null) names.AddRange(aliases);
            SupportedNames = names.ToArray();
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal sealed class DeviceState
    {
        public string ChargeMode;
        public string SupportedChargeModes;
        public string PerformanceMode;
        public string SupportedPerformanceModes;
        public string WorkingDriver;
        public string ShowGeekAsCreator;
        public string ShowBsmAsQuietBsm;
        public string IsGeekOptionGrey;
        public string ErrorCode;
        public string ChargeError;
        public string ChargeBackend;
        public string ChargeLimitInfo;
        public string ThresholdError;
        public string PerformanceError;
        public string KeyboardBacklightStatus;
        public string KeyboardBacklightLevelCapability;
        public string KeyboardBacklightReserve;
        public string KeyboardBacklightAutoDimCapability;
        public string KeyboardBacklightAutoDimStatus;
        public string KeyboardBacklightTimeout;
        public string KeyboardBacklightError;
        public string KeyboardBacklightAgent;
        public string AddinInfo;
        public bool ChargeWritable;
        public bool ThresholdCapable;
        public bool ThresholdEnabled;
        public bool ThresholdWritable;
        public int ThresholdStart;
        public int ThresholdStop;
        public bool PerformanceWritable;
        public bool KeyboardBacklightSupported;
        public bool KeyboardBacklightWritable;
        public bool KeyboardBacklightReserveWritable;
        public bool KeyboardBacklightAutoDimWritable;
    }

    internal sealed class LenovoAddinClient
    {
        private readonly LenovoOptionalFeaturesClient optionalClient = new LenovoOptionalFeaturesClient();
        private readonly ChargeThresholdClient thresholdClient = new ChargeThresholdClient();
        private readonly EnergyDriverChargeClient directChargeClient = new EnergyDriverChargeClient();
        private readonly LenovoKeyboardBacklightClient keyboardBacklightClient = new LenovoKeyboardBacklightClient();
        private bool directChargeActive;

        public DeviceState ReadState()
        {
            var state = new DeviceState
            {
                AddinInfo = AddinLocator.DescribeAssembly(AddinLocator.FindAssembly())
            };
            try
            {
                DirectChargeState direct = directChargeClient.Read();
                state.ChargeMode = direct.Mode.ToString();
                state.SupportedChargeModes = direct.SupportedModes;
                state.ChargeWritable = true;
                state.ChargeBackend = "直接驱动";
                state.ChargeLimitInfo = direct.LimitDescription;
                directChargeActive = true;
            }
            catch (Exception directError)
            {
                directChargeActive = false;
                try
                {
                    object charge = optionalClient.ReadCharge();
                    state.ChargeMode = AddinResponse.ReadSetting(charge, "BatteryChargeMode");
                    state.SupportedChargeModes = AddinResponse.ReadSetting(charge,
                        "Supported-BatteryChargeMode", "SupportedBatteryChargeMode", "BatteryChargeModeSupported");
                    string chargeErrorCode = AddinResponse.ErrorCode(charge);
                    if (!String.IsNullOrWhiteSpace(chargeErrorCode) && chargeErrorCode != "0")
                        state.ChargeError = "设备返回错误 " + chargeErrorCode;
                    state.ChargeWritable = optionalClient.HasMethod("SetBatteryChargeMode");
                    state.ChargeBackend = "Lenovo Addin";
                }
                catch (Exception addinError)
                {
                    state.ChargeError = "直接驱动：" + RootMessage(directError) + "；Addin：" + RootMessage(addinError);
                }
            }
            try
            {
                ChargeThresholdState threshold = thresholdClient.Read(0);
                state.ThresholdCapable = threshold.IsCapable;
                state.ThresholdEnabled = threshold.IsEnabled;
                state.ThresholdWritable = threshold.IsWritable;
                state.ThresholdStart = threshold.StartValue;
                state.ThresholdStop = threshold.StopValue;
            }
            catch (Exception ex) { state.ThresholdError = RootMessage(ex); }
            try
            {
                object performance = optionalClient.ReadPerformance();
                state.PerformanceMode = AddinResponse.ReadSetting(performance, "ITSMode");
                state.SupportedPerformanceModes = AddinResponse.ReadSetting(performance,
                    "Supported-ITSMode", "SupportedITSMode", "ITSModeSupported");
                state.WorkingDriver = AddinResponse.ReadSetting(performance, "WorkingDriver");
                state.ShowGeekAsCreator = AddinResponse.ReadSetting(performance, "ShowGeekAsCreator");
                state.ShowBsmAsQuietBsm = AddinResponse.ReadSetting(performance, "ShowBsmAsQuietBsm");
                state.IsGeekOptionGrey = AddinResponse.ReadSetting(performance, "IsGeekOptionGrey");
                state.ErrorCode = AddinResponse.ReadSetting(performance, "ErrorCode");
                if (!String.IsNullOrWhiteSpace(state.ErrorCode) && state.ErrorCode != "0")
                    state.PerformanceError = "设备返回错误 " + state.ErrorCode;
                state.PerformanceWritable = optionalClient.HasMethod("SetITSMode");
            }
            catch (Exception ex) { state.PerformanceError = RootMessage(ex); }
            try
            {
                KeyboardBacklightState backlight = keyboardBacklightClient.Read();
                state.KeyboardBacklightSupported = backlight.IsSupported;
                state.KeyboardBacklightWritable = backlight.IsWritable;
                state.KeyboardBacklightReserveWritable = backlight.CanReserve;
                state.KeyboardBacklightAutoDimWritable = backlight.CanAutoDim;
                state.KeyboardBacklightStatus = backlight.Status;
                state.KeyboardBacklightLevelCapability = backlight.LevelCapability;
                state.KeyboardBacklightReserve = backlight.Reserve;
                state.KeyboardBacklightAutoDimCapability = backlight.AutoDimCapability;
                state.KeyboardBacklightAutoDimStatus = backlight.AutoDimStatus;
                state.KeyboardBacklightTimeout = backlight.Timeout;
                state.KeyboardBacklightError = backlight.Error;
                state.KeyboardBacklightAgent = backlight.AgentInfo;
            }
            catch (Exception ex) { state.KeyboardBacklightError = RootMessage(ex); }
            return state;
        }

        public object SetCharge(ChargeMode mode)
        {
            if (directChargeActive)
            {
                DirectChargeState result = directChargeClient.SetMode(
                    mode == ChargeMode.Storage ? DirectChargeMode.Storage :
                    mode == ChargeMode.Quick ? DirectChargeMode.Quick : DirectChargeMode.Normal);
                return new Dictionary<string, object>
                {
                    { "ErrorCode", "0" }, { "backend", "EnergyDrv" }, { "mode", result.Mode.ToString() }
                };
            }
            return optionalClient.SetCharge(mode);
        }

        public object SetPerformance(PerformanceMode mode) { return optionalClient.SetPerformance(mode); }

        public void SetThreshold(int startValue, int stopValue) { thresholdClient.Set(0, startValue, stopValue); }

        public object SetKeyboardBacklight(KeyboardBacklightLevel level)
        {
            return keyboardBacklightClient.SetLevel(level);
        }

        public object SetKeyboardBacklightReserve(bool enabled)
        {
            return keyboardBacklightClient.SetReserve(enabled);
        }

        public object SetKeyboardBacklightAutoDim(bool enabled)
        {
            return keyboardBacklightClient.SetAutoDim(enabled);
        }

        public object RestoreKeyboardBacklightDefault()
        {
            return keyboardBacklightClient.RestoreDefault();
        }

        private static string RootMessage(Exception exception)
        {
            Exception cause = exception;
            while (cause.InnerException != null) cause = cause.InnerException;
            return cause.Message;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly LenovoAddinClient client = new LenovoAddinClient();
        private readonly FlowLayoutPanel chargeModes = new FlowLayoutPanel();
        private readonly FlowLayoutPanel performanceModes = new FlowLayoutPanel();
        private readonly FlowLayoutPanel keyboardBacklightModes = new FlowLayoutPanel();
        private readonly FlowLayoutPanel keyboardBacklightActions = new FlowLayoutPanel();
        private readonly Label chargeCurrent = new Label();
        private readonly Label chargeSupported = new Label();
        private readonly Label thresholdCurrent = new Label();
        private readonly Label thresholdSupported = new Label();
        private readonly FlowLayoutPanel thresholdControls = new FlowLayoutPanel();
        private readonly NumericUpDown thresholdStart = new NumericUpDown();
        private readonly NumericUpDown thresholdStop = new NumericUpDown();
        private readonly Button thresholdApply = new Button();
        private readonly Label performanceCurrent = new Label();
        private readonly Label performanceSupported = new Label();
        private readonly Label keyboardBacklightCurrent = new Label();
        private readonly Label keyboardBacklightSupported = new Label();
        private readonly Button keyboardBacklightReserveButton = new Button();
        private readonly Button keyboardBacklightAutoDimButton = new Button();
        private readonly Button keyboardBacklightDefaultButton = new Button();
        private readonly Label driverValue = new Label();
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
            BackColor = Color.FromArgb(244, 246, 249);
            ForeColor = Color.FromArgb(32, 35, 42);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 700);
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

        private static TableLayoutPanel AutoTable(int columns)
        {
            return new TableLayoutPanel
            {
                ColumnCount = columns,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        private static void StyleButton(Button button, string text, bool primary)
        {
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(104, 38);
            button.Padding = new Padding(14, 6, 14, 6);
            button.Margin = Padding.Empty;
            button.Anchor = AnchorStyles.Right;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(212, 219, 229);
            button.BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(55, 65, 81);
            button.Cursor = Cursors.Hand;
        }

        private void BuildLayout()
        {
            var page = AutoTable(1);
            page.Dock = DockStyle.Fill;
            page.AutoSize = false;
            page.AutoScroll = true;
            page.Padding = new Padding(24, 16, 24, 16);
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowCount = 6;
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = AutoTable(1);
            header.Margin = new Padding(0, 0, 0, 16);
            header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var heading = AutoTable(1);
            heading.Controls.Add(new Label
            {
                Text = "EnergyControl for Lenovo",
                Font = new Font(Font.FontFamily, 19F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);
            heading.Controls.Add(new Label
            {
                Text = "Unofficial · v0.1.0-preview.1 · 无遥测",
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 110, 125),
                Margin = Padding.Empty
            }, 0, 1);
            StyleButton(refreshButton, "刷新状态", false);
            refreshButton.Click += async delegate { await RefreshStateAsync(); };
            StyleButton(aboutButton, "关于", false);
            aboutButton.Click += delegate { ShowAbout(); };
            StyleButton(diagnosticsButton, "诊断", false);
            diagnosticsButton.Click += delegate { ShowDiagnostics(); };
            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                FlowDirection = FlowDirection.LeftToRight
            };
            actions.Controls.Add(aboutButton);
            actions.Controls.Add(diagnosticsButton);
            actions.Controls.Add(refreshButton);
            header.Controls.Add(heading, 0, 0);
            header.Controls.Add(actions, 0, 1);

            page.Controls.Add(header, 0, 0);
            page.Controls.Add(CreateCard("充电模式 · 稳定", chargeCurrent,
                chargeSupported, chargeModes), 0, 1);
            page.Controls.Add(CreateThresholdCard(), 0, 2);
            page.Controls.Add(CreateCard("性能管理 · 实验", performanceCurrent,
                performanceSupported, performanceModes), 0, 3);
            page.Controls.Add(CreateKeyboardBacklightCard(), 0, 4);

            var footer = AutoTable(2);
            footer.Margin = new Padding(0, 8, 0, 0);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            driverValue.AutoSize = true;
            driverValue.ForeColor = Color.FromArgb(100, 110, 125);
            driverValue.Margin = Padding.Empty;
            statusLabel.AutoSize = true;
            statusLabel.Margin = new Padding(12, 0, 0, 0);
            statusLabel.Anchor = AnchorStyles.Right;
            footer.Controls.Add(driverValue, 0, 0);
            footer.Controls.Add(statusLabel, 1, 0);
            page.Controls.Add(footer, 0, 5);
            Controls.Add(page);
        }

        private TableLayoutPanel CreateKeyboardBacklightCard()
        {
            var card = AutoTable(1);
            card.BackColor = Color.White;
            card.Padding = new Padding(18);
            card.Margin = new Padding(0, 0, 0, 10);
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowCount = 5;
            for (int index = 0; index < 5; index++)
                card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(new Label
            {
                Text = "键盘背光 · 实验",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 9)
            }, 0, 0);

            keyboardBacklightCurrent.Text = "当前：读取中…";
            keyboardBacklightCurrent.AutoSize = true;
            keyboardBacklightCurrent.Margin = new Padding(0, 0, 0, 5);
            card.Controls.Add(keyboardBacklightCurrent, 0, 1);

            keyboardBacklightSupported.Text = "支持：读取中…";
            keyboardBacklightSupported.AutoSize = true;
            keyboardBacklightSupported.ForeColor = Color.FromArgb(100, 110, 125);
            keyboardBacklightSupported.Margin = new Padding(0, 0, 0, 10);
            card.Controls.Add(keyboardBacklightSupported, 0, 2);

            keyboardBacklightModes.AutoSize = true;
            keyboardBacklightModes.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            keyboardBacklightModes.Dock = DockStyle.Top;
            keyboardBacklightModes.WrapContents = true;
            keyboardBacklightModes.Margin = Padding.Empty;
            keyboardBacklightModes.Padding = Padding.Empty;
            keyboardBacklightModes.AccessibleName = "键盘背光亮度选项";
            card.Controls.Add(keyboardBacklightModes, 0, 3);

            keyboardBacklightActions.AutoSize = true;
            keyboardBacklightActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            keyboardBacklightActions.Dock = DockStyle.Top;
            keyboardBacklightActions.WrapContents = true;
            keyboardBacklightActions.Margin = Padding.Empty;
            keyboardBacklightActions.Padding = Padding.Empty;
            StyleButton(keyboardBacklightReserveButton, "切换保留状态", false);
            StyleButton(keyboardBacklightAutoDimButton, "切换自动调暗", false);
            StyleButton(keyboardBacklightDefaultButton, "恢复默认", false);
            keyboardBacklightReserveButton.Margin = new Padding(0, 0, 10, 8);
            keyboardBacklightAutoDimButton.Margin = new Padding(0, 0, 10, 8);
            keyboardBacklightDefaultButton.Margin = new Padding(0, 0, 10, 8);
            keyboardBacklightReserveButton.Click += async delegate
            {
                if (busy) return;
                if (MessageBox.Show(this, "确认切换键盘背光保留状态吗？", "确认设置",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await ApplyKeyboardBacklightReserveAsync(!IsTrue(keyboardBacklightReserveButton.Tag));
            };
            keyboardBacklightAutoDimButton.Click += async delegate
            {
                if (busy) return;
                if (MessageBox.Show(this, "确认切换键盘背光自动调暗吗？", "确认设置",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await ApplyKeyboardBacklightAutoDimAsync(!IsTrue(keyboardBacklightAutoDimButton.Tag));
            };
            keyboardBacklightDefaultButton.Click += async delegate
            {
                if (busy) return;
                if (MessageBox.Show(this, "确认恢复键盘背光默认设置吗？", "确认设置",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RestoreKeyboardBacklightDefaultAsync();
            };
            keyboardBacklightActions.Controls.Add(keyboardBacklightReserveButton);
            keyboardBacklightActions.Controls.Add(keyboardBacklightAutoDimButton);
            keyboardBacklightActions.Controls.Add(keyboardBacklightDefaultButton);
            card.Controls.Add(keyboardBacklightActions, 0, 4);
            return card;
        }

        private TableLayoutPanel CreateThresholdCard()
        {
            var card = AutoTable(1);
            card.BackColor = Color.White;
            card.Padding = new Padding(18);
            card.Margin = new Padding(0, 0, 0, 10);
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowCount = 4;
            for (int index = 0; index < 4; index++)
                card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(new Label
            {
                Text = "自定义充电阈值 · 实验",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 9)
            }, 0, 0);

            thresholdCurrent.Text = "当前：读取中…";
            thresholdCurrent.AutoSize = true;
            thresholdCurrent.Margin = new Padding(0, 0, 0, 5);
            card.Controls.Add(thresholdCurrent, 0, 1);

            thresholdSupported.Text = "支持：读取中…";
            thresholdSupported.AutoSize = true;
            thresholdSupported.ForeColor = Color.FromArgb(100, 110, 125);
            thresholdSupported.Margin = new Padding(0, 0, 0, 10);
            card.Controls.Add(thresholdSupported, 0, 2);

            ConfigureThresholdInput(thresholdStart, 75, 0);
            ConfigureThresholdInput(thresholdStop, 80, 1);
            StyleButton(thresholdApply, "应用阈值", true);
            thresholdApply.Margin = new Padding(8, 0, 0, 0);
            thresholdApply.Click += async delegate
            {
                if (busy) return;
                int startValue = Decimal.ToInt32(thresholdStart.Value);
                int stopValue = Decimal.ToInt32(thresholdStop.Value);
                try
                {
                    ChargeThresholdClient.ValidateValues(startValue, stopValue);
                }
                catch (Exception ex)
                {
                    ShowError("阈值无效", ex);
                    return;
                }
                if (MessageBox.Show(
                    this,
                    "确认设置为低于 " + startValue + "% 开始充电，充到 " +
                        stopValue + "% 停止吗？",
                    "确认设置",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                await ApplyThresholdAsync(startValue, stopValue);
            };

            thresholdControls.AutoSize = true;
            thresholdControls.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            thresholdControls.Dock = DockStyle.Top;
            thresholdControls.WrapContents = true;
            thresholdControls.Margin = Padding.Empty;
            thresholdControls.Padding = Padding.Empty;
            thresholdControls.Visible = false;
            thresholdControls.Enabled = false;
            thresholdControls.AccessibleName = "自定义充电阈值设置";
            thresholdControls.Controls.Add(CreateInlineLabel("低于"));
            thresholdControls.Controls.Add(thresholdStart);
            thresholdControls.Controls.Add(CreateInlineLabel("% 开始，充到"));
            thresholdControls.Controls.Add(thresholdStop);
            thresholdControls.Controls.Add(CreateInlineLabel("% 停止"));
            thresholdControls.Controls.Add(thresholdApply);
            card.Controls.Add(thresholdControls, 0, 3);
            return card;
        }

        private static void ConfigureThresholdInput(
            NumericUpDown input, int value, int minimum)
        {
            input.Minimum = minimum;
            input.Maximum = 100;
            input.Value = value;
            input.Width = 64;
            input.TextAlign = HorizontalAlignment.Center;
            input.Margin = new Padding(5, 5, 5, 0);
        }

        private static Label CreateInlineLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 9, 0, 0)
            };
        }

        private TableLayoutPanel CreateCard(string title, Label current,
            Label supported, FlowLayoutPanel modes)
        {
            var card = AutoTable(1);
            card.BackColor = Color.White;
            card.Padding = new Padding(18);
            card.Margin = new Padding(0, 0, 0, 10);
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowCount = 4;
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 9)
            }, 0, 0);

            current.Text = "当前：读取中…";
            current.AutoSize = true;
            current.Margin = new Padding(0, 0, 0, 5);
            card.Controls.Add(current, 0, 1);

            supported.Text = "支持：读取中…";
            supported.AutoSize = true;
            supported.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            supported.ForeColor = Color.FromArgb(100, 110, 125);
            supported.Margin = new Padding(0, 0, 0, 10);
            card.Controls.Add(supported, 0, 2);

            modes.AutoSize = true;
            modes.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            modes.Dock = DockStyle.Top;
            modes.WrapContents = true;
            modes.Margin = Padding.Empty;
            modes.Padding = Padding.Empty;
            modes.AccessibleName = title + "选项";
            card.Controls.Add(modes, 0, 3);
            return card;
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
                if (!String.IsNullOrWhiteSpace(state.ErrorCode) &&
                    state.ErrorCode != "0")
                    SetStatus(
                        "设备返回错误 " + state.ErrorCode,
                        Color.FromArgb(180, 50, 45));
                else if (unavailable == 3)
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

        private async Task ApplyChargeAsync(ModeItem selected)
        {
            if (selected == null) return;
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
                {
                    DeviceState before = client.ReadState();
                    if (!String.IsNullOrWhiteSpace(before.ChargeError) ||
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
                DisplayState(state);
                SetStatus("充电模式已更新", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("设置充电模式失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task ApplyPerformanceAsync(ModeItem selected)
        {
            if (selected == null) return;
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
                {
                    DeviceState before = client.ReadState();
                    if (!String.IsNullOrWhiteSpace(before.PerformanceError) ||
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
                DisplayState(state);
                SetStatus("性能模式已更新", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("设置性能模式失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task ApplyThresholdAsync(int startValue, int stopValue)
        {
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
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
                DisplayState(state);
                SetStatus("充电阈值已更新", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("设置充电阈值失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task ApplyKeyboardBacklightAsync(ModeItem selected)
        {
            if (selected == null) return;
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
                {
                    DeviceState before = client.ReadState();
                    if (!before.KeyboardBacklightSupported ||
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
                DisplayState(state);
                SetStatus("键盘背光已更新", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("设置键盘背光失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task ApplyKeyboardBacklightReserveAsync(bool enabled)
        {
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
                {
                    DeviceState before = client.ReadState();
                    if (!before.KeyboardBacklightSupported ||
                        !before.KeyboardBacklightReserveWritable)
                        throw new InvalidOperationException("设备当前不支持键盘背光保留状态。");
                    object response = client.SetKeyboardBacklightReserve(enabled);
                    EnsureKeyboardResponseSuccess(response, "键盘背光保留状态");
                    DeviceState result = client.ReadState();
                    if (!IsBooleanValue(result.KeyboardBacklightReserve, enabled))
                        throw new InvalidOperationException("设备未接受键盘背光保留状态设置。");
                    return result;
                });
                DisplayState(state);
                SetStatus("键盘背光保留状态已更新", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("设置键盘背光保留状态失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task ApplyKeyboardBacklightAutoDimAsync(bool enabled)
        {
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
                {
                    DeviceState before = client.ReadState();
                    if (!before.KeyboardBacklightSupported ||
                        !before.KeyboardBacklightAutoDimWritable)
                        throw new InvalidOperationException("设备当前不支持键盘背光自动调暗。");
                    object response = client.SetKeyboardBacklightAutoDim(enabled);
                    EnsureKeyboardResponseSuccess(response, "键盘背光自动调暗");
                    DeviceState result = client.ReadState();
                    if (!IsBooleanValue(result.KeyboardBacklightAutoDimStatus, enabled))
                        throw new InvalidOperationException("设备未接受键盘背光自动调暗设置。");
                    return result;
                });
                DisplayState(state);
                SetStatus("键盘背光自动调暗已更新", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("设置键盘背光自动调暗失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task RestoreKeyboardBacklightDefaultAsync()
        {
            SetBusy(true, "正在应用...");
            try
            {
                DeviceState state = await Task.Run(() =>
                {
                    DeviceState before = client.ReadState();
                    if (!before.KeyboardBacklightSupported || !before.KeyboardBacklightWritable)
                        throw new InvalidOperationException("设备当前不支持恢复键盘背光默认设置。");
                    object response = client.RestoreKeyboardBacklightDefault();
                    EnsureKeyboardResponseSuccess(response, "键盘背光恢复默认");
                    return client.ReadState();
                });
                DisplayState(state);
                SetStatus("键盘背光已恢复默认", Color.FromArgb(30, 125, 78));
            }
            catch (Exception ex)
            {
                ShowError("恢复键盘背光默认设置失败", ex);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void DisplayState(DeviceState state)
        {
            if (state == null) return;
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
                chargeModes.Controls.Clear();
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
                    thresholdStop.Value = ClampThreshold(state.ThresholdStop, 0, 100);
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
                performanceModes.Controls.Clear();
            }

            if (String.IsNullOrWhiteSpace(state.KeyboardBacklightError) &&
                state.KeyboardBacklightSupported)
            {
                keyboardBacklightCurrent.Text = "当前：" +
                    KeyboardBacklightNames.DisplayName(state.KeyboardBacklightStatus);
                keyboardBacklightSupported.Text =
                    "支持：" + DisplayKeyboardBacklightSupported(state.KeyboardBacklightLevelCapability) +
                    (state.KeyboardBacklightWritable ? "" : "（只读）") +
                    (String.IsNullOrWhiteSpace(state.KeyboardBacklightAgent)
                        ? ""
                        : "  ·  " + state.KeyboardBacklightAgent);
                FillKeyboardBacklightModes(
                    keyboardBacklightModes,
                    state.KeyboardBacklightLevelCapability,
                    state.KeyboardBacklightStatus,
                    state.KeyboardBacklightWritable);
                keyboardBacklightReserveButton.Tag = state.KeyboardBacklightReserve;
                keyboardBacklightReserveButton.Text = "保留状态：" +
                    (IsTrue(state.KeyboardBacklightReserve) ? "开" : "关");
                keyboardBacklightReserveButton.Enabled = state.KeyboardBacklightReserveWritable && !busy;
                keyboardBacklightAutoDimButton.Tag = state.KeyboardBacklightAutoDimStatus;
                keyboardBacklightAutoDimButton.Text = "自动调暗：" +
                    (IsTrue(state.KeyboardBacklightAutoDimStatus) ? "开" : "关");
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
                keyboardBacklightModes.Controls.Clear();
                keyboardBacklightReserveButton.Enabled = false;
                keyboardBacklightAutoDimButton.Enabled = false;
                keyboardBacklightDefaultButton.Enabled = false;
            }

            string driver = String.IsNullOrWhiteSpace(state.WorkingDriver)
                ? "驱动：未知"
                : "驱动：" + state.WorkingDriver;
            driverValue.Text = driver +
                (String.IsNullOrWhiteSpace(state.AddinInfo)
                    ? ""
                    : "  |  Addin：" + state.AddinInfo);
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
                panel.Controls.Clear();
                if (!writable) return;
                foreach (ModeItem item in allModes)
                {
                    if (ContainsMode(supported, item) &&
                        !(item.ContractName == "MMC_Geek" && geekOptionGrey))
                    {
                        Button button = new Button { Tag = item };
                        StyleButton(button, item.DisplayName, IsCurrent(item, current));
                        button.Anchor = AnchorStyles.Left;
                        button.Margin = new Padding(0, 0, 10, 8);
                        button.AccessibleName = item.DisplayName;
                         button.Click += async delegate
                         {
                             if (!busy)
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
                panel.Controls.Clear();
                if (!writable) return;
                foreach (ModeItem item in AllKeyboardBacklightModes)
                {
                    if (!SupportsKeyboardBacklightMode(capability, item)) continue;
                    Button button = new Button { Tag = item };
                    StyleButton(button, item.DisplayName, IsCurrent(item, current));
                    button.Anchor = AnchorStyles.Left;
                    button.Margin = new Padding(0, 0, 10, 8);
                    button.AccessibleName = item.DisplayName;
                    button.Click += async delegate
                    {
                        if (busy) return;
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
            if (String.IsNullOrWhiteSpace(value)) return false;
            return IsTrue(value) == expected;
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
            if (value.IndexOf("IdeaNotebookAddin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Vantage", StringComparison.OrdinalIgnoreCase) >= 0)
                return "未检测到 Lenovo Vantage/百应组件";
            const int maxLength = 28;
            return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
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
                "Direct charging is the stable path. Performance controls and custom percentage thresholds are experimental.\n" +
                "This application does not include Lenovo private components, does not send telemetry, and does not represent Lenovo.",
                "关于 EnergyControl",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowDiagnostics()
        {
            string direct;
            try
            {
                DirectChargeState state = new EnergyDriverChargeClient().Read();
                direct = "可用；模式=" + state.Mode + "；标志=0x" + state.RawFlags.ToString("X8");
            }
            catch (Exception ex)
            {
                direct = "不可用：" + ex.Message;
            }
            string text =
                "硬件：" + AddinLocator.HardwareSummary() + "\n" +
                "直接充电驱动：" + direct + "\n" +
                "Lenovo Addin：" + AddinLocator.DescribeAssembly(AddinLocator.FindAssembly()) + "\n" +
                "Power RPC：" + ChargeThresholdClient.DescribeAvailability() + "\n\n" +
                "诊断只读，不会写入充电设置。";
            MessageBox.Show(this, text, "诊断", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            UseWaitCursor = value;
            if (message != null)
                SetStatus(message, Color.FromArgb(80, 85, 95));
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
