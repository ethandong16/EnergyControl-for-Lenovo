using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Lenovo.Modern.Contracts.BatteryManagement;
using Lenovo.Modern.Contracts.Power;
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
        public string PerformanceError;
        public string AddinInfo;
        public bool ChargeWritable;
        public bool PerformanceWritable;
    }

    internal sealed class LenovoAddinClient
    {
        private const string AddinAssemblyName = "IdeaNotebookAddin.dll";
        private const string AgentTypeName = "IdeaNotebookAddin.IdeaNotebookAgent";
        private object agent;
        private Type agentType;

        public DeviceState ReadState()
        {
            var state = new DeviceState
            {
                AddinInfo = AddinLocator.DescribeAssembly(AddinLocator.FindAssembly())
            };
            try
            {
                object charge = Invoke("GetBatteryChargeMode");
                state.ChargeMode = ReadSetting(charge, "BatteryChargeMode");
                state.SupportedChargeModes = ReadSetting(
                    charge,
                    "Supported-BatteryChargeMode",
                    "SupportedBatteryChargeMode",
                    "BatteryChargeModeSupported");
                string chargeErrorCode = AddinResponse.ErrorCode(charge);
                if (!String.IsNullOrWhiteSpace(chargeErrorCode) && chargeErrorCode != "0")
                    state.ChargeError = "设备返回错误 " + chargeErrorCode;
                state.ChargeWritable = HasMethod(
                    "SetBatteryChargeMode", typeof(BatteryMgmtRequest));
            }
            catch (Exception ex)
            {
                state.ChargeError = RootMessage(ex);
            }
            try
            {
                object performance = Invoke("GetITSMode", new PowerSettingsRequest
                {
                    UISupportGeekMode = true
                });
                state.PerformanceMode = ReadSetting(performance, "ITSMode");
                state.SupportedPerformanceModes = ReadSetting(
                    performance,
                    "Supported-ITSMode",
                    "SupportedITSMode",
                    "ITSModeSupported");
                state.WorkingDriver = ReadSetting(performance, "WorkingDriver");
                state.ShowGeekAsCreator = ReadSetting(performance, "ShowGeekAsCreator");
                state.ShowBsmAsQuietBsm = ReadSetting(performance, "ShowBsmAsQuietBsm");
                state.IsGeekOptionGrey = ReadSetting(performance, "IsGeekOptionGrey");
                state.ErrorCode = ReadSetting(performance, "ErrorCode");
                if (!String.IsNullOrWhiteSpace(state.ErrorCode) && state.ErrorCode != "0")
                    state.PerformanceError = "设备返回错误 " + state.ErrorCode;
                state.PerformanceWritable = HasMethod(
                    "SetITSMode", typeof(PowerSettingsRequest));
            }
            catch (Exception ex)
            {
                state.PerformanceError = RootMessage(ex);
            }
            return state;
        }

        public object SetCharge(BatteryChargeModeType mode)
        {
            return Invoke("SetBatteryChargeMode", new BatteryMgmtRequest
            {
                BatteryChargeMode = mode
            });
        }

        public object SetPerformance(ItsModeType mode)
        {
            return Invoke("SetITSMode", new PowerSettingsRequest
            {
                ItsMode = mode,
                UISupportGeekMode = true
            });
        }

        private object Invoke(string methodName, params object[] arguments)
        {
            EnsureAgent();
            MethodInfo method = FindMethod(methodName, arguments);
            if (method == null)
                throw new MissingMethodException(agentType.FullName, methodName);
            try
            {
                return method.Invoke(agent, arguments);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private void EnsureAgent()
        {
            if (agent != null) return;

            string path = AddinLocator.FindAssembly();
            if (String.IsNullOrWhiteSpace(path))
                throw new FileNotFoundException(
                    "未检测到 Lenovo Vantage/百应的 IdeaNotebookAddin。此电脑可能不是联想设备，或相关服务未安装。",
                    AddinAssemblyName);
            Assembly addin = Assembly.LoadFrom(path);
            agentType = AddinLocator.FindAgentType(addin);
            if (agentType == null)
                throw new MissingMethodException(
                    "未在 Addin 中找到兼容的设备代理类型。可能是商用 Vantage 或不匹配的版本。");
            MethodInfo getInstance = agentType.GetMethod(
                "GetInstance",
                BindingFlags.Public | BindingFlags.Static);
            if (getInstance == null)
                throw new MissingMethodException(AgentTypeName, "GetInstance");
            agent = getInstance.Invoke(null, null);
            if (agent == null)
                throw new InvalidOperationException(
                    "IdeaNotebookAgent.GetInstance() returned null.");
        }

        private static string ReadSetting(object response, params string[] keys)
        {
            return AddinResponse.ReadSetting(response, keys);
        }

        private MethodInfo FindMethod(string methodName, object[] arguments)
        {
            foreach (MethodInfo candidate in agentType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (!String.Equals(candidate.Name, methodName, StringComparison.Ordinal) ||
                    candidate.GetParameters().Length != arguments.Length)
                    continue;
                ParameterInfo[] parameters = candidate.GetParameters();
                bool matches = true;
                for (int index = 0; index < parameters.Length; index++)
                {
                    if (arguments[index] == null)
                    {
                        if (parameters[index].ParameterType.IsValueType) matches = false;
                    }
                    else if (!parameters[index].ParameterType.IsAssignableFrom(arguments[index].GetType()))
                    {
                        matches = false;
                    }
                }
                if (matches) return candidate;
            }
            return null;
        }

        private bool HasMethod(string methodName, Type argumentType)
        {
            foreach (MethodInfo candidate in agentType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (!String.Equals(candidate.Name, methodName, StringComparison.Ordinal) ||
                    candidate.GetParameters().Length != 1)
                    continue;
                if (candidate.GetParameters()[0].ParameterType.IsAssignableFrom(argumentType))
                    return true;
            }
            return false;
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
        private readonly Label chargeCurrent = new Label();
        private readonly Label chargeSupported = new Label();
        private readonly Label performanceCurrent = new Label();
        private readonly Label performanceSupported = new Label();
        private readonly Label driverValue = new Label();
        private readonly Label statusLabel = new Label();
        private readonly Button refreshButton = new Button();
        private bool busy;
        private bool geekOptionGrey;

        private static readonly ModeItem[] AllChargeModes =
        {
            new ModeItem("常规充电", "Normal", BatteryChargeModeType.Normal,
                "Standard", "Regular"),
            new ModeItem("养护充电", "Storage", BatteryChargeModeType.Storage,
                "Conservation", "BatteryConservation", "LongLife"),
            new ModeItem("快充", "Quick", BatteryChargeModeType.Quick,
                "Express", "Rapid", "RapidCharge")
        };

        private static readonly ModeItem[] AllPerformanceModes =
        {
            new ModeItem("自动", "MMC_Auto", ItsModeType.ItsAuto,
                "ITS_Auto", "MMC_Balance", "Auto", "Balance", "Balanced", "Smart",
                "IntelligentCooling"),
            new ModeItem("安静 / 节能", "MMC_Cool", ItsModeType.MmcCool,
                "MMC_Quiet", "Quiet", "Silent", "Cool", "Bsm_Quiet", "BatterySaving", "EnergySaving"),
            new ModeItem(
                "高性能",
                "MMC_Performance",
                ItsModeType.MmcPerformance,
                "MMC_Extreme", "Performance", "Extreme", "Turbo"),
            new ModeItem("极客模式", "MMC_Geek", ItsModeType.MmcGeek,
                "Geek", "Creator", "Creative")
        };

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "联想设备设置";
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = Color.FromArgb(244, 246, 249);
            ForeColor = Color.FromArgb(32, 35, 42);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 540);
            MinimumSize = new Size(560, 540);
            BuildLayout();
            SetBusy(true, "正在读取...");
            ResumeLayout(true);
            Shown += async delegate
            {
                busy = false;
                await RefreshStateAsync();
            };
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
            page.AutoScroll = false;
            page.Padding = new Padding(24, 16, 24, 16);
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowCount = 4;
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = AutoTable(2);
            header.Margin = new Padding(0, 0, 0, 16);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var heading = AutoTable(1);
            heading.Controls.Add(new Label
            {
                Text = "联想设备设置",
                Font = new Font(Font.FontFamily, 19F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);
            heading.Controls.Add(new Label
            {
                Text = "选择适合你的充电与性能模式",
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 110, 125),
                Margin = Padding.Empty
            }, 0, 1);
            StyleButton(refreshButton, "刷新状态", false);
            refreshButton.Click += async delegate { await RefreshStateAsync(); };
            header.Controls.Add(heading, 0, 0);
            header.Controls.Add(refreshButton, 1, 0);

            page.Controls.Add(header, 0, 0);
            page.Controls.Add(CreateCard("充电模式", chargeCurrent,
                chargeSupported, chargeModes), 0, 1);
            page.Controls.Add(CreateCard("性能管理", performanceCurrent,
                performanceSupported, performanceModes), 0, 2);

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
            page.Controls.Add(footer, 0, 3);
            Controls.Add(page);
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
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
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

            modes.AutoSize = false;
            modes.Height = 46;
            modes.Dock = DockStyle.Top;
            modes.WrapContents = false;
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
                if (!String.IsNullOrWhiteSpace(state.ErrorCode) &&
                    state.ErrorCode != "0")
                    SetStatus(
                        "设备返回错误 " + state.ErrorCode,
                        Color.FromArgb(180, 50, 45));
                else if (!String.IsNullOrWhiteSpace(state.ChargeError) &&
                    !String.IsNullOrWhiteSpace(state.PerformanceError))
                    SetStatus("设备设置接口不可用", Color.FromArgb(180, 50, 45));
                else if (!String.IsNullOrWhiteSpace(state.ChargeError) ||
                    !String.IsNullOrWhiteSpace(state.PerformanceError))
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
                    object response = client.SetCharge((BatteryChargeModeType)selected.Value);
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
                    object response = client.SetPerformance((ItsModeType)selected.Value);
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
                        (state.ChargeWritable ? "" : "（只读）");
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

        private static string ShortMessage(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "未报告";
            if (value.IndexOf("IdeaNotebookAddin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Vantage", StringComparison.OrdinalIgnoreCase) >= 0)
                return "未检测到 Lenovo Vantage/百应组件";
            const int maxLength = 28;
            return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            refreshButton.Enabled = !value;
            chargeModes.Enabled = !value && chargeModes.Controls.Count > 0;
            performanceModes.Enabled =
                !value && performanceModes.Controls.Count > 0;
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

    internal static class GuiProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
