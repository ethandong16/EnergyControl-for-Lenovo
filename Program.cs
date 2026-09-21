using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LenovoSettingsCompat;

namespace LenovoSettingsDemo
{
    internal static class Program
    {
        private static readonly Dictionary<string, ChargeMode> ChargeModes =
            new Dictionary<string, ChargeMode>(StringComparer.OrdinalIgnoreCase)
            {
                { "normal", ChargeMode.Normal },
                { "conservation", ChargeMode.Storage },
                { "storage", ChargeMode.Storage },
                { "express", ChargeMode.Quick },
                { "quick", ChargeMode.Quick }
            };

        private static readonly Dictionary<string, PerformanceMode> PerformanceModes =
            new Dictionary<string, PerformanceMode>(StringComparer.OrdinalIgnoreCase)
            {
                { "auto", PerformanceMode.Auto },
                { "quiet", PerformanceMode.Cool },
                { "cool", PerformanceMode.Cool },
                { "battery-saving", PerformanceMode.Cool },
                { "performance", PerformanceMode.Performance },
                { "geek", PerformanceMode.Geek }
            };

        private static readonly Dictionary<string, KeyboardBacklightLevel> KeyboardBacklightModes =
            new Dictionary<string, KeyboardBacklightLevel>(StringComparer.OrdinalIgnoreCase)
            {
                { "off", KeyboardBacklightLevel.Off },
                { "disabled-off", KeyboardBacklightLevel.DisabledOff },
                { "level1", KeyboardBacklightLevel.Level1 },
                { "level_1", KeyboardBacklightLevel.Level1 },
                { "level2", KeyboardBacklightLevel.Level2 },
                { "level_2", KeyboardBacklightLevel.Level2 },
                { "auto", KeyboardBacklightLevel.Auto }
            };

        private static readonly LenovoOptionalFeaturesClient optionalClient = new LenovoOptionalFeaturesClient();
        private static readonly ChargeThresholdClient thresholdClient = new ChargeThresholdClient();
        private static readonly EnergyDriverChargeClient directChargeClient = new EnergyDriverChargeClient();
        private static readonly LenovoKeyboardBacklightClient keyboardBacklightClient = new LenovoKeyboardBacklightClient();

        internal static int Run(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0 || EqualsArg(args[0], "status")) return ShowStatus();
                if (EqualsArg(args[0], "charge")) return HandleCharge(args);
                if (EqualsArg(args[0], "performance")) return HandlePerformance(args);
                if (EqualsArg(args[0], "keyboard-backlight") || EqualsArg(args[0], "backlight"))
                    return HandleKeyboardBacklight(args);
                if (EqualsArg(args[0], "diagnose") || EqualsArg(args[0], "capabilities")) return ShowDiagnostics();
                if (EqualsArg(args[0], "help") || EqualsArg(args[0], "--help") || EqualsArg(args[0], "-h"))
                {
                    PrintUsage();
                    return 0;
                }
                Console.Error.WriteLine("Unknown command: " + args[0]);
                PrintUsage();
                return 2;
            }
            catch (TargetInvocationException ex)
            {
                Console.Error.WriteLine("Operation failed: " + (ex.InnerException ?? ex).Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Operation failed: " + RootMessage(ex));
                return 1;
            }
        }

        private static int ShowStatus()
        {
            Console.WriteLine("== EnergyControl for Lenovo ==");
            Console.WriteLine("Unofficial preview; stable direct charge, experimental optional features");
            Console.WriteLine("OS: " + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ", process: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            Console.WriteLine("Hardware: " + AddinLocator.HardwareSummary());
            Console.WriteLine();
            bool readAny = false;
            Console.WriteLine("== Direct charging (stable) ==");
            try { PrintDirectCharge(directChargeClient.Read()); readAny = true; }
            catch (Exception ex) { Console.WriteLine("Unavailable: " + RootMessage(ex)); }
            Console.WriteLine();
            Console.WriteLine("== Lenovo Addin charging (experimental fallback) ==");
            try { PrintResponse(optionalClient.ReadCharge()); readAny = true; }
            catch (Exception ex) { Console.WriteLine("Unavailable: " + RootMessage(ex)); }
            Console.WriteLine();
            Console.WriteLine("== Custom charge threshold (experimental) ==");
            try { PrintThreshold(thresholdClient.Read(0)); readAny = true; }
            catch (Exception ex) { Console.WriteLine("Unavailable: " + RootMessage(ex)); }
            Console.WriteLine();
            Console.WriteLine("== Performance management (experimental) ==");
            try { PrintResponse(optionalClient.ReadPerformance()); readAny = true; }
            catch (Exception ex) { Console.WriteLine("Unavailable: " + RootMessage(ex)); }
            Console.WriteLine();
            Console.WriteLine("== Keyboard backlight (experimental) ==");
            try { PrintKeyboardBacklight(keyboardBacklightClient.Read()); readAny = true; }
            catch (Exception ex) { Console.WriteLine("Unavailable: " + RootMessage(ex)); }
            return readAny ? 0 : 1;
        }

        private static int HandleKeyboardBacklight(string[] args)
        {
            if (args.Length == 2 &&
                (EqualsArg(args[1], "get") || EqualsArg(args[1], "status") ||
                 EqualsArg(args[1], "capability") || EqualsArg(args[1], "capabilities")))
            {
                PrintKeyboardBacklight(keyboardBacklightClient.Read());
                return 0;
            }
            if (args.Length >= 3 && EqualsArg(args[1], "set"))
            {
                KeyboardBacklightLevel level;
                if (!KeyboardBacklightModes.TryGetValue(args[2], out level))
                    return InvalidMode("keyboard-backlight");
                RequireApply(args);
                KeyboardBacklightState before = keyboardBacklightClient.Read();
                EnsureKeyboardBacklightWritable(before);
                EnsureKeyboardBacklightLevelSupported(before, level);
                Console.WriteLine("Before:");
                PrintKeyboardBacklight(before);
                object response = keyboardBacklightClient.SetLevel(level);
                EnsureKeyboardResponseSuccess(response, "键盘背光");
                Console.WriteLine("Set response:");
                PrintResponse(response);
                KeyboardBacklightState after = keyboardBacklightClient.Read();
                EnsureKeyboardBacklightCurrent(after, level);
                Console.WriteLine("After:");
                PrintKeyboardBacklight(after);
                return 0;
            }
            if (args.Length >= 3 &&
                (EqualsArg(args[1], "reserve") || EqualsArg(args[1], "auto-dim")))
            {
                bool enabled;
                if (EqualsArg(args[2], "on")) enabled = true;
                else if (EqualsArg(args[2], "off")) enabled = false;
                else throw new ArgumentException("reserve/auto-dim must be on or off.");
                RequireApply(args);
                KeyboardBacklightState before = keyboardBacklightClient.Read();
                EnsureKeyboardBacklightWritable(before);
                bool reserve = EqualsArg(args[1], "reserve");
                if (reserve && !before.CanReserve)
                    throw new InvalidOperationException("设备未提供键盘背光保留状态写入接口。");
                if (!reserve && !before.CanAutoDim)
                    throw new InvalidOperationException("设备未报告键盘背光自动调暗能力。");
                Console.WriteLine("Before:");
                PrintKeyboardBacklight(before);
                object response = reserve
                    ? keyboardBacklightClient.SetReserve(enabled)
                    : keyboardBacklightClient.SetAutoDim(enabled);
                EnsureKeyboardResponseSuccess(response, reserve ? "键盘背光保留状态" : "键盘背光自动调暗");
                Console.WriteLine("Set response:");
                PrintResponse(response);
                Console.WriteLine("After:");
                PrintKeyboardBacklight(keyboardBacklightClient.Read());
                return 0;
            }
            if (args.Length >= 2 && EqualsArg(args[1], "restore-default"))
            {
                RequireApply(args);
                KeyboardBacklightState before = keyboardBacklightClient.Read();
                EnsureKeyboardBacklightWritable(before);
                Console.WriteLine("Before:");
                PrintKeyboardBacklight(before);
                object response = keyboardBacklightClient.RestoreDefault();
                EnsureKeyboardResponseSuccess(response, "键盘背光恢复默认");
                Console.WriteLine("Set response:");
                PrintResponse(response);
                Console.WriteLine("After:");
                PrintKeyboardBacklight(keyboardBacklightClient.Read());
                return 0;
            }
            PrintUsage();
            return 2;
        }

        private static int HandleCharge(string[] args)
        {
            if (args.Length >= 3 && EqualsArg(args[1], "direct")) return HandleDirectCharge(args);
            if (args.Length >= 3 && EqualsArg(args[1], "threshold")) return HandleChargeThreshold(args);
            if (args.Length == 2 && EqualsArg(args[1], "get")) { PrintResponse(optionalClient.ReadCharge()); return 0; }
            if (args.Length >= 3 && EqualsArg(args[1], "set"))
            {
                ChargeMode mode;
                if (!ChargeModes.TryGetValue(args[2], out mode)) return InvalidMode("charge");
                RequireApply(args);
                Console.WriteLine("Before:");
                object before = optionalClient.ReadCharge();
                PrintResponse(before);
                EnsureResponseSuccess(before, "读取充电能力");
                EnsureChargeSupported(before, mode);
                Console.WriteLine("Set response:");
                object response = optionalClient.SetCharge(mode);
                PrintResponse(response);
                EnsureResponseSuccess(response, "充电模式");
                Console.WriteLine("After:");
                PrintResponse(optionalClient.ReadCharge());
                return 0;
            }
            PrintUsage();
            return 2;
        }

        private static int HandleDirectCharge(string[] args)
        {
            if (args.Length == 3 && EqualsArg(args[2], "get")) { PrintDirectCharge(directChargeClient.Read()); return 0; }
            if (args.Length >= 4 && EqualsArg(args[2], "set"))
            {
                ChargeMode mode;
                if (!ChargeModes.TryGetValue(args[3], out mode)) return InvalidMode("charge");
                RequireApply(args);
                Console.WriteLine("Before:");
                PrintDirectCharge(directChargeClient.Read());
                Console.WriteLine("After:");
                PrintDirectCharge(directChargeClient.SetMode(ToDirectMode(mode)));
                return 0;
            }
            PrintUsage();
            return 2;
        }

        private static DirectChargeMode ToDirectMode(ChargeMode mode)
        {
            switch (mode)
            {
                case ChargeMode.Storage: return DirectChargeMode.Storage;
                case ChargeMode.Quick: return DirectChargeMode.Quick;
                default: return DirectChargeMode.Normal;
            }
        }

        private static void PrintDirectCharge(DirectChargeState state)
        {
            var output = new Dictionary<string, object>
            {
                { "backend", "EnergyDrv" },
                { "protocol", EnergyDriverChargeClient.DescribeProtocol() },
                { "mode", state.Mode.ToString() },
                { "supportedModes", state.SupportedModes },
                { "storageEnabled", state.StorageEnabled },
                { "quickEnabled", state.QuickEnabled },
                { "quickCapable", state.QuickCapable },
                { "storage80Capable", state.Storage80Capable },
                { "storageLimit", state.LimitDescription },
                { "rawFlags", "0x" + state.RawFlags.ToString("X8") }
            };
            Console.WriteLine(JsonSupport.Serialize(output, true));
        }

        private static int HandleChargeThreshold(string[] args)
        {
            const int slot = 0;
            if (args.Length == 3 && EqualsArg(args[2], "get")) { PrintThreshold(thresholdClient.Read(slot)); return 0; }
            if (args.Length >= 5 && EqualsArg(args[2], "set"))
            {
                int startValue;
                int stopValue;
                if (!Int32.TryParse(args[3], out startValue) || !Int32.TryParse(args[4], out stopValue))
                    throw new ArgumentException("起充和停充阈值必须是整数百分比。");
                ChargeThresholdClient.ValidateValues(startValue, stopValue);
                RequireApply(args);
                Console.WriteLine("Before:");
                ChargeThresholdState before = thresholdClient.Read(slot);
                PrintThreshold(before);
                if (!before.IsCapable) throw new InvalidOperationException("设备未报告支持自定义充电阈值。");
                if (!before.IsWritable) throw new InvalidOperationException("当前 Power RPC 客户端不提供阈值写入接口。");
                thresholdClient.Set(slot, startValue, stopValue);
                Console.WriteLine("Set response:\n0");
                Console.WriteLine("After:");
                ChargeThresholdState after = thresholdClient.Read(slot);
                PrintThreshold(after);
                if (!after.IsEnabled || after.StartValue != startValue || after.StopValue != stopValue)
                    throw new InvalidOperationException("设备未启用或未接受请求的阈值；当前为 " + after.StartValue + "% / " + after.StopValue + "% 。");
                return 0;
            }
            PrintUsage();
            return 2;
        }

        private static void PrintThreshold(ChargeThresholdState state)
        {
            var output = new Dictionary<string, object>
            {
                { "slot", state.Slot }, { "capable", state.IsCapable }, { "enabled", state.IsEnabled },
                { "startPercent", state.StartValue }, { "stopPercent", state.StopValue },
                { "writable", state.IsWritable }, { "client", state.ClientInfo }
            };
            Console.WriteLine(JsonSupport.Serialize(output, true));
        }

        private static void PrintKeyboardBacklight(KeyboardBacklightState state)
        {
            var output = new Dictionary<string, object>
            {
                { "supported", state.IsSupported },
                { "writable", state.IsWritable },
                { "reserveWritable", state.CanReserve },
                { "autoDimWritable", state.CanAutoDim },
                { "status", state.Status },
                { "statusDisplay", KeyboardBacklightNames.DisplayName(state.Status) },
                { "levelCapability", state.LevelCapability },
                { "reserve", state.Reserve },
                { "autoDimCapability", state.AutoDimCapability },
                { "autoDimStatus", state.AutoDimStatus },
                { "timeout", state.Timeout },
                { "agent", state.AgentInfo },
                { "error", state.Error },
                { "capabilities", state.Capabilities },
                { "settings", state.Settings }
            };
            Console.WriteLine(JsonSupport.Serialize(output, true));
        }

        private static void EnsureKeyboardBacklightWritable(KeyboardBacklightState state)
        {
            if (state == null || !state.IsSupported)
                throw new InvalidOperationException("设备未报告键盘背光能力。" +
                    (state == null || String.IsNullOrWhiteSpace(state.Error) ? "" : " " + state.Error));
            if (!String.IsNullOrWhiteSpace(state.Error))
                throw new InvalidOperationException("读取键盘背光状态失败：" + state.Error);
            if (!state.IsWritable)
                throw new InvalidOperationException("当前 Lenovo Addin 未提供键盘背光写入接口。");
        }

        private static void EnsureKeyboardBacklightLevelSupported(
            KeyboardBacklightState state, KeyboardBacklightLevel requested)
        {
            string capability = state.LevelCapability ?? "";
            if (requested == KeyboardBacklightLevel.Off || requested == KeyboardBacklightLevel.DisabledOff)
                return;
            if (requested == KeyboardBacklightLevel.Level1 &&
                (capability.IndexOf("OneLevel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 capability.IndexOf("TwoLevels", StringComparison.OrdinalIgnoreCase) >= 0))
                return;
            if (requested == KeyboardBacklightLevel.Level2 &&
                (capability.IndexOf("TwoLevels", StringComparison.OrdinalIgnoreCase) >= 0))
                return;
            if (requested == KeyboardBacklightLevel.Auto &&
                capability.IndexOf("Auto", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
            throw new InvalidOperationException(
                "设备未报告支持该键盘背光档位（KeyboardBacklightLevel=" +
                (String.IsNullOrWhiteSpace(capability) ? "未报告" : capability) + "）。");
        }

        private static void EnsureKeyboardBacklightCurrent(
            KeyboardBacklightState state, KeyboardBacklightLevel requested)
        {
            string expected = KeyboardBacklightNames.ToContractValue(requested);
            if (state == null || !String.Equals(state.Status, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "设备未接受该键盘背光设置；当前为 " +
                    KeyboardBacklightNames.DisplayName(state == null ? null : state.Status) + "。");
        }

        private static void EnsureKeyboardResponseSuccess(object response, string feature)
        {
            if (response is bool && !(bool)response)
                throw new InvalidOperationException(feature + "设置被设备拒绝。");
            if (!KeyboardBacklightResponse.IsSuccess(response))
                throw new InvalidOperationException(feature + "设置被设备拒绝（ErrorCode=" +
                    KeyboardBacklightResponse.ErrorCode(response) + "）。");
        }

        private static int HandlePerformance(string[] args)
        {
            if (args.Length == 2 && EqualsArg(args[1], "get")) { PrintResponse(optionalClient.ReadPerformance()); return 0; }
            if (args.Length >= 3 && EqualsArg(args[1], "set"))
            {
                PerformanceMode mode;
                if (!PerformanceModes.TryGetValue(args[2], out mode)) return InvalidMode("performance");
                RequireApply(args);
                object before = optionalClient.ReadPerformance();
                Console.WriteLine("Before:");
                PrintResponse(before);
                EnsureResponseSuccess(before, "读取性能能力");
                EnsurePerformanceSupported(before, mode);
                Console.WriteLine("Set response:");
                object response = optionalClient.SetPerformance(mode);
                PrintResponse(response);
                EnsureResponseSuccess(response, "性能模式");
                Console.WriteLine("After:");
                PrintResponse(optionalClient.ReadPerformance());
                return 0;
            }
            if (args.Length >= 3 && EqualsArg(args[1], "auto-transition"))
            {
                bool enabled;
                if (EqualsArg(args[2], "on")) enabled = true;
                else if (EqualsArg(args[2], "off")) enabled = false;
                else throw new ArgumentException("auto-transition must be on or off.");
                RequireApply(args);
                object response = optionalClient.SetAutoTransition(enabled);
                Console.WriteLine("Set response:");
                PrintResponse(response);
                EnsureResponseSuccess(response, "自动切换");
                Console.WriteLine("After:");
                PrintResponse(optionalClient.ReadPerformance());
                return 0;
            }
            PrintUsage();
            return 2;
        }

        private static void EnsureChargeSupported(object response, ChargeMode mode)
        {
            string supported = AddinResponse.ReadSetting(response, "Supported-BatteryChargeMode", "SupportedBatteryChargeMode", "BatteryChargeModeSupported");
            string[] aliases;
            switch (mode)
            {
                case ChargeMode.Storage: aliases = new[] { "Storage", "Conservation", "BatteryConservation", "LongLife" }; break;
                case ChargeMode.Quick: aliases = new[] { "Quick", "Express", "Rapid", "RapidCharge" }; break;
                default: aliases = new[] { "Normal", "Standard", "Regular" }; break;
            }
            if (!CapabilityNames.Matches(supported, aliases))
                throw new InvalidOperationException("设备未报告支持该充电模式（Supported-BatteryChargeMode=" + (String.IsNullOrWhiteSpace(supported) ? "未报告" : supported) + "）。");
        }

        private static void EnsurePerformanceSupported(object response, PerformanceMode mode)
        {
            string supported = AddinResponse.ReadSetting(response, "Supported-ITSMode", "SupportedITSMode", "ITSModeSupported");
            string[] aliases;
            switch (mode)
            {
                case PerformanceMode.Auto: aliases = new[] { "ITS_Auto", "MMC_Auto", "MMC_Balance", "Auto", "Balance", "Balanced", "Smart", "IntelligentCooling" }; break;
                case PerformanceMode.Cool: aliases = new[] { "MMC_Cool", "MMC_Quiet", "Quiet", "Silent", "Cool", "Bsm_Quiet", "BatterySaving", "EnergySaving" }; break;
                case PerformanceMode.Performance: aliases = new[] { "MMC_Performance", "MMC_Extreme", "Performance", "Extreme", "Turbo" }; break;
                default: aliases = new[] { "MMC_Geek", "Geek", "Creator", "Creative" }; break;
            }
            if (!CapabilityNames.Matches(supported, aliases))
                throw new InvalidOperationException("设备未报告支持该性能模式（Supported-ITSMode=" + (String.IsNullOrWhiteSpace(supported) ? "未报告" : supported) + "）。");
        }

        private static void EnsureResponseSuccess(object response, string feature)
        {
            if (!AddinResponse.IsSuccess(response))
                throw new InvalidOperationException(feature + "设置被设备拒绝（ErrorCode=" + (AddinResponse.ErrorCode(response) ?? "未知") + "）。");
        }

        private static int ShowDiagnostics()
        {
            Console.WriteLine("EnergyControl for Lenovo diagnostics");
            Console.WriteLine("OS architecture: " + (Environment.Is64BitOperatingSystem ? "x64" : "x86"));
            Console.WriteLine("Process architecture: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            Console.WriteLine("Hardware: " + AddinLocator.HardwareSummary());
            Console.WriteLine("IdeaNotebookAddin: " + AddinLocator.DescribeAssembly(AddinLocator.FindAssembly()));
            Console.WriteLine("Charge threshold RPC: " + ChargeThresholdClient.DescribeAvailability());
            bool directAvailable = false;
            try
            {
                DirectChargeState direct = directChargeClient.Read();
                directAvailable = true;
                Console.WriteLine("Direct charge driver: available (" + EnergyDriverChargeClient.DescribeProtocol() + ")");
                Console.WriteLine("Direct charge flags: 0x" + direct.RawFlags.ToString("X8") + ", mode=" + direct.Mode + ", storage80=" + direct.Storage80Capable);
            }
            catch (Exception ex) { Console.WriteLine("Direct charge driver: unavailable - " + RootMessage(ex)); }
            try
            {
                Console.WriteLine("Agent: " + optionalClient.AgentDescription);
                Console.WriteLine("GetBatteryChargeMode: " + (optionalClient.HasMethod("GetBatteryChargeMode") ? "yes" : "no"));
                Console.WriteLine("GetITSMode: " + (optionalClient.HasMethod("GetITSMode") ? "yes" : "no"));
                Console.WriteLine("IsSupportBacklight: " + (keyboardBacklightClient.HasMethod("IsSupportBacklight") ? "yes" : "no"));
                Console.WriteLine("GetBacklightStatus: " + (keyboardBacklightClient.HasMethod("GetBacklightStatus") ? "yes" : "no"));
                Console.WriteLine("SetBacklightStatus: " + (keyboardBacklightClient.HasMethod("SetBacklightStatus") ? "yes" : "no"));
            }
            catch (Exception ex) { Console.WriteLine("Optional Addin: unavailable - " + RootMessage(ex)); }
            Console.WriteLine("Result: " + (directAvailable ? "direct stable path available" : "no stable direct path detected"));
            return directAvailable ? 0 : 1;
        }

        private static void PrintResponse(object response)
        {
            if (response == null) { Console.WriteLine("null"); return; }
            string json = AddinResponse.ToJson(response);
            try { Console.WriteLine(JsonSupport.Serialize(JsonSupport.Deserialize(json), true)); }
            catch { Console.WriteLine(json); }
        }

        private static string RootMessage(Exception exception)
        {
            Exception cause = exception;
            while (cause.InnerException != null) cause = cause.InnerException;
            return cause.Message;
        }

        private static void RequireApply(string[] args)
        {
            foreach (string arg in args) if (EqualsArg(arg, "--apply")) return;
            throw new InvalidOperationException("Write operation blocked. Re-run with --apply after checking the requested mode.");
        }

        private static int InvalidMode(string category)
        {
            Console.Error.WriteLine("Unsupported " + category + " mode. Run with --help to see valid modes.");
            return 2;
        }

        private static bool EqualsArg(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("EnergyControl for Lenovo - unofficial Windows 10/11 x64 utility");
            Console.WriteLine();
            Console.WriteLine("Read-only:");
            Console.WriteLine("  EnergyControl.exe status");
            Console.WriteLine("  EnergyControl.exe charge get");
            Console.WriteLine("  EnergyControl.exe charge direct get");
            Console.WriteLine("  EnergyControl.exe charge threshold get");
            Console.WriteLine("  EnergyControl.exe performance get");
            Console.WriteLine("  EnergyControl.exe keyboard-backlight get");
            Console.WriteLine("  EnergyControl.exe keyboard-backlight capability");
            Console.WriteLine("  EnergyControl.exe diagnose");
            Console.WriteLine();
            Console.WriteLine("Writes (explicit --apply required):");
            Console.WriteLine("  EnergyControl.exe charge set normal|conservation|express --apply");
            Console.WriteLine("  EnergyControl.exe charge direct set normal|conservation|express --apply");
            Console.WriteLine("  EnergyControl.exe charge threshold set <start-percent> <stop-percent> --apply");
            Console.WriteLine("  EnergyControl.exe performance set auto|quiet|performance|geek --apply");
            Console.WriteLine("  EnergyControl.exe performance auto-transition on|off --apply");
            Console.WriteLine("  EnergyControl.exe keyboard-backlight set off|level1|level2|auto --apply");
            Console.WriteLine("  EnergyControl.exe keyboard-backlight reserve on|off --apply");
            Console.WriteLine("  EnergyControl.exe keyboard-backlight auto-dim on|off --apply");
            Console.WriteLine("  EnergyControl.exe keyboard-backlight restore-default --apply");
        }
    }
}
