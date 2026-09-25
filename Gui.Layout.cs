using System;
using System.Drawing;
using System.Windows.Forms;
using LenovoSettingsCompat;

namespace LenovoSettingsGui
{
    // TableLayoutPanel measures wrapping flows with unconstrained width first.
    // Use the assigned width so its rows do not reserve space for a vertical stack.
    internal sealed class WrappingFlowPanel : FlowLayoutPanel
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            return base.GetPreferredSize(new Size(Width > 1 ? Width : proposedSize.Width, 0));
        }
    }

    internal sealed partial class MainForm
    {
        private static TableLayoutPanel AutoTable(int columns)
        {
            return new TableLayoutPanel
            {
                ColumnCount = columns,
                Size = new Size(1, 1),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        private static void StyleButton(ButtonBase button, string text, bool primary)
        {
            button.Text = text;
            button.AutoSize = true;
            if (button is Button) ((Button)button).AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(104, 38);
            button.Padding = new Padding(14, 6, 14, 6);
            button.Margin = Padding.Empty;
            button.Anchor = AnchorStyles.Right;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(212, 219, 229);
            button.FlatAppearance.CheckedBackColor = Color.FromArgb(0, 110, 90);
            button.FlatAppearance.MouseOverBackColor = primary
                ? Color.FromArgb(0, 95, 78) : Color.FromArgb(237, 246, 244);
            button.BackColor = primary ? Color.FromArgb(0, 110, 90) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(55, 65, 81);
            // Inherit the system cursor; .NET Framework's Hand ignores user cursor sizing.
        }

        private void BuildLayout()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(20, 16, 20, 12), BackColor = Color.White
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = AutoTable(1);
            header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var brand = new Label
            {
                Text = "EnergyControl for Lenovo", AutoSize = true,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            header.Controls.Add(brand, 0, 0);
            var toolbar = AutoTable(3);
            toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int index = 0; index < 3; index++)
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.Margin = new Padding(0, 0, 0, 14);
            StyleButton(refreshButton, UiText.Get("刷新"), true);
            StyleButton(aboutButton, UiText.Get("关于"), false);
            StyleButton(helpButton, UiText.Get("帮助"), false);
            foreach (Button button in new[] { refreshButton, helpButton, aboutButton })
                button.Margin = new Padding(0, 0, 8, 0);
            refreshButton.Click += async delegate { await RefreshStateAsync(); };
            aboutButton.Click += delegate { ShowAbout(); };
            helpButton.Click += delegate { ShowHelp(); };
            toolbar.Controls.AddRange(new Control[] { refreshButton, helpButton, aboutButton });
            header.Controls.Add(toolbar, 0, 1);
            shell.Controls.Add(header, 0, 0);

            settingsTabs.Dock = DockStyle.Fill;
            settingsTabs.Padding = new Point(18, 8);
            settingsTabs.AccessibleName = UiText.Get("设备设置");
            AddSettingsPage(UiText.Get("电池"),
                CreateSection(UiText.Get("充电模式"), chargeCurrent, chargeSupported, chargeModes),
                CreateThresholdSection());
            AddSettingsPage(UiText.Get("性能"),
                CreateSection(UiText.Get("性能模式"), performanceCurrent, performanceSupported, performanceModes));
            AddSettingsPage(UiText.Get("键盘"), CreateKeyboardBacklightSection());

            diagnosticsPage.Text = UiText.Get("诊断");
            diagnosticsPage.BackColor = Color.White;
            diagnosticsPage.Padding = new Padding(12);
            diagnosticsText.Dock = DockStyle.Fill;
            diagnosticsText.Multiline = true;
            diagnosticsText.ReadOnly = true;
            diagnosticsText.ScrollBars = ScrollBars.Vertical;
            diagnosticsText.BorderStyle = BorderStyle.None;
            diagnosticsText.BackColor = Color.White;
            diagnosticsText.AccessibleName = UiText.Get("设备诊断报告");
            diagnosticsText.Text = UiText.Get("尚未读取设备状态");
            diagnosticsPage.Controls.Add(diagnosticsText);
            settingsTabs.TabPages.Add(diagnosticsPage);
            shell.Controls.Add(settingsTabs, 0, 1);

            var footer = AutoTable(1);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.Padding = new Padding(0, 10, 0, 0);
            statusLabel.AutoSize = true;
            statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            statusLabel.Margin = Padding.Empty;
            footer.Controls.Add(statusLabel, 0, 0);
            shell.Controls.Add(footer, 0, 2);
            Controls.Add(shell);
        }

        private void AddSettingsPage(string title, params Control[] sections)
        {
            var tab = new TabPage(title) { BackColor = Color.White, Padding = new Padding(4) };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var content = AutoTable(1);
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowCount = sections.Length;
            foreach (Control section in sections)
                content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            foreach (Control section in sections)
                content.Controls.Add(section, 0, content.Controls.Count);
            scroll.Controls.Add(content);
            tab.Controls.Add(scroll);
            settingsTabs.TabPages.Add(tab);
        }

        private TableLayoutPanel CreateKeyboardBacklightSection()
        {
            var card = AutoTable(1);
            card.BackColor = Color.White;
            card.Padding = new Padding(14, 18, 14, 18);
            card.Margin = Padding.Empty;
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowCount = 5;
            for (int index = 0; index < 5; index++)
                card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(new Label
            {
                Text = UiText.Get("键盘背光"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 9)
            }, 0, 0);

            keyboardBacklightCurrent.Text = UiText.Get("当前：读取中…");
            keyboardBacklightCurrent.AutoSize = true;
            keyboardBacklightCurrent.Margin = new Padding(0, 0, 0, 5);
            card.Controls.Add(keyboardBacklightCurrent, 0, 1);

            keyboardBacklightSupported.Text = UiText.Get("支持：读取中…");
            keyboardBacklightSupported.AutoSize = true;
            keyboardBacklightSupported.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            keyboardBacklightSupported.ForeColor = Color.FromArgb(100, 110, 125);
            keyboardBacklightSupported.Margin = new Padding(0, 0, 0, 10);
            card.Controls.Add(keyboardBacklightSupported, 0, 2);

            keyboardBacklightModes.AutoSize = true;
            keyboardBacklightModes.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            keyboardBacklightModes.Dock = DockStyle.Top;
            keyboardBacklightModes.WrapContents = true;
            keyboardBacklightModes.Margin = Padding.Empty;
            keyboardBacklightModes.Padding = Padding.Empty;
            keyboardBacklightModes.AccessibleName = UiText.Get("键盘背光亮度选项");
            card.Controls.Add(keyboardBacklightModes, 0, 3);

            keyboardBacklightActions.AutoSize = true;
            keyboardBacklightActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            keyboardBacklightActions.Dock = DockStyle.Top;
            keyboardBacklightActions.WrapContents = true;
            keyboardBacklightActions.Margin = Padding.Empty;
            keyboardBacklightActions.Padding = Padding.Empty;
            StyleToggle(keyboardBacklightReserveButton, UiText.Get("记住背光状态"));
            StyleToggle(keyboardBacklightAutoDimButton, UiText.Get("自动调暗"));
            StyleButton(keyboardBacklightDefaultButton, UiText.Get("恢复默认"), false);
            keyboardBacklightReserveButton.Margin = new Padding(0, 0, 10, 8);
            keyboardBacklightAutoDimButton.Margin = new Padding(0, 0, 10, 8);
            keyboardBacklightDefaultButton.Margin = new Padding(0, 0, 10, 8);
            keyboardBacklightReserveButton.Click += async delegate
            {
                if (busy) return;
                if (MessageBox.Show(this, UiText.Get("确认切换键盘背光保留状态吗？"), UiText.Get("确认设置"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await ApplyKeyboardBacklightReserveAsync(!IsTrue(keyboardBacklightReserveButton.Tag));
            };
            keyboardBacklightAutoDimButton.Click += async delegate
            {
                if (busy) return;
                if (MessageBox.Show(this, UiText.Get("确认切换键盘背光自动调暗吗？"), UiText.Get("确认设置"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await ApplyKeyboardBacklightAutoDimAsync(!IsTrue(keyboardBacklightAutoDimButton.Tag));
            };
            keyboardBacklightDefaultButton.Click += async delegate
            {
                if (busy) return;
                if (MessageBox.Show(this, UiText.Get("确认恢复键盘背光默认设置吗？"), UiText.Get("确认设置"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RestoreKeyboardBacklightDefaultAsync();
            };
            keyboardBacklightActions.Controls.Add(keyboardBacklightReserveButton);
            keyboardBacklightActions.Controls.Add(keyboardBacklightAutoDimButton);
            keyboardBacklightActions.Controls.Add(keyboardBacklightDefaultButton);
            card.Controls.Add(keyboardBacklightActions, 0, 4);
            return card;
        }

        private TableLayoutPanel CreateThresholdSection()
        {
            var card = AutoTable(1);
            card.BackColor = Color.White;
            card.Padding = new Padding(14, 18, 14, 18);
            card.Margin = Padding.Empty;
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowCount = 4;
            for (int index = 0; index < 4; index++)
                card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(new Label
            {
                Text = UiText.Get("充电阈值"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 9)
            }, 0, 0);

            thresholdCurrent.Text = UiText.Get("当前：读取中…");
            thresholdCurrent.AutoSize = true;
            thresholdCurrent.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            thresholdCurrent.Margin = new Padding(0, 0, 0, 5);
            card.Controls.Add(thresholdCurrent, 0, 1);

            thresholdSupported.Text = UiText.Get("支持：读取中…");
            thresholdSupported.AutoSize = true;
            thresholdSupported.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            thresholdSupported.ForeColor = Color.FromArgb(100, 110, 125);
            thresholdSupported.Margin = new Padding(0, 0, 0, 10);
            card.Controls.Add(thresholdSupported, 0, 2);

            ConfigureThresholdInput(thresholdStart, 75, 0);
            ConfigureThresholdInput(thresholdStop, 80, 1);
            StyleButton(thresholdApply, UiText.Get("应用阈值"), true);
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
                    ShowError(UiText.Get("阈值无效"), ex);
                    return;
                }
                if (MessageBox.Show(
                    this,
                    UiText.Get("确认设置为低于 ") + startValue + UiText.Get("% 开始充电，充到 ") +
                        stopValue + UiText.Get("% 停止吗？"),
                    UiText.Get("确认设置"),
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
            thresholdControls.AccessibleName = UiText.Get("自定义充电阈值设置");
            thresholdControls.Controls.Add(CreateInlineLabel(UiText.Get("低于")));
            thresholdControls.Controls.Add(thresholdStart);
            thresholdControls.Controls.Add(CreateInlineLabel(UiText.Get("% 开始，充到")));
            thresholdControls.Controls.Add(thresholdStop);
            thresholdControls.Controls.Add(CreateInlineLabel(UiText.Get("% 停止")));
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

        private TableLayoutPanel CreateSection(string title, Label current,
            Label supported, FlowLayoutPanel modes)
        {
            var card = AutoTable(1);
            card.BackColor = Color.White;
            card.Padding = new Padding(14, 18, 14, 18);
            card.Margin = Padding.Empty;
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

            current.Text = UiText.Get("当前：读取中…");
            current.AutoSize = true;
            current.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            current.Margin = new Padding(0, 0, 0, 5);
            card.Controls.Add(current, 0, 1);

            supported.Text = UiText.Get("支持：读取中…");
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
            modes.AccessibleName = title + UiText.Get("选项");
            card.Controls.Add(modes, 0, 3);
            return card;
        }

        private static void StyleToggle(CheckBox toggle, string text)
        {
            toggle.Text = text;
            toggle.AutoSize = true;
            toggle.AutoCheck = false;
            toggle.Padding = new Padding(0, 8, 8, 8);
            toggle.AccessibleName = text;
        }

        private void UpdateDiagnostics(DeviceState state)
        {
            diagnosticsText.Text = String.Join(Environment.NewLine, new[]
            {
                "EnergyControl for Lenovo",
                UiText.Get("读取时间：") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "",
                UiText.Get("充电模式：") + (state.ChargeMode ?? UiText.Get("未知")),
                UiText.Get("充电后端：") + (state.ChargeBackend ?? UiText.Get("不可用")),
                UiText.Get("充电错误：") + (state.ChargeError ?? UiText.Get("无")),
                UiText.Get("阈值能力：") + UiText.Get(state.ThresholdCapable ? "是" : "否"),
                UiText.Get("阈值错误：") + (state.ThresholdError ?? UiText.Get("无")),
                "",
                UiText.Get("性能模式：") + (state.PerformanceMode ?? UiText.Get("未知")),
                UiText.Get("性能错误：") + (state.PerformanceError ?? UiText.Get("无")),
                UiText.Get("性能驱动：") + (state.WorkingDriver ?? UiText.Get("未知")),
                "",
                UiText.Get("背光状态：") + (state.KeyboardBacklightStatus ?? UiText.Get("未知")),
                UiText.Get("背光档位：") + (state.KeyboardBacklightLevelCapability ?? UiText.Get("未报告")),
                UiText.Get("保留状态：") + (state.KeyboardBacklightReserve ?? UiText.Get("未报告")),
                UiText.Get("自动调暗能力：") + (state.KeyboardBacklightAutoDimCapability ?? UiText.Get("未报告")),
                UiText.Get("自动调暗状态：") + (state.KeyboardBacklightAutoDimStatus ?? UiText.Get("未报告")),
                UiText.Get("背光错误：") + (state.KeyboardBacklightError ?? UiText.Get("无")),
                "",
                UiText.Get("Lenovo Addin：") + (state.AddinInfo ?? UiText.Get("未找到"))
            });
        }

    }
}
