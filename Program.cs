using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lenovo.Modern.Contracts.BatteryManagement;
using Lenovo.Modern.Contracts.Power;
using LenovoSettingsCompat;

namespace LenovoSettingsDemo
{
    internal static class Program
    {
        private const string AddinAssemblyName = "IdeaNotebookAddin.dll";
        private const string AgentTypeName = "IdeaNotebookAddin.IdeaNotebookAgent";

        private static readonly Dictionary<string, BatteryChargeModeType> ChargeModes =
            new Dictionary<string, BatteryChargeModeType>(StringComparer.OrdinalIgnoreCase)
            {
                { "normal", BatteryChargeModeType.Normal },
                { "conservation", BatteryChargeModeType.Storage },
                { "storage", BatteryChargeModeType.Storage },
                { "express", BatteryChargeModeType.Quick },
                { "quick", BatteryChargeModeType.Quick }
            };

        private static readonly Dictionary<string, ItsModeType> PerformanceModes =
            new Dictionary<string, ItsModeType>(StringComparer.OrdinalIgnoreCase)
            {
                { "auto", ItsModeType.ItsAuto },
                { "quiet", ItsModeType.MmcCool },
                { "cool", ItsModeType.MmcCool },
                { "battery-saving", ItsModeType.MmcCool },
                { "performance", ItsModeType.MmcPerformance },
                { "geek", ItsModeType.MmcGeek }
            };

        private static object agent;
        private static Type agentType;

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || EqualsArg(args[0], "status"))
                    return ShowStatus();
                if (EqualsArg(args[0], "charge"))
                    return HandleCharge(args);
                if (EqualsArg(args[0], "performance"))
                    return HandlePerformance(args);
                if (EqualsArg(args[0], "diagnose") || EqualsArg(args[0], "capabilities"))
                    return ShowDiagnostics();
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
                Exception cause = ex.InnerException ?? ex;
                Console.Error.WriteLine("Lenovo Addin call failed: " + cause.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Lenovo Addin call failed: " + ex.Message);
                return 1;
            }
        }

        private static int ShowStatus()
        {
            Console.WriteLine("== Compatibility ==");
            string addinPath = AddinLocator.FindAssembly();
            Console.WriteLine("Addin: " + AddinLocator.DescribeAssembly(addinPath));
            Console.WriteLine("OS: " + (Environment.Is64BitOperatingSystem ? "x64" : "x86") +
                ", process: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            Console.WriteLine();

            bool readAny = false;
            Console.WriteLine("== Charging mode ==");
            try
            {
                PrintResponse(InvokeAgent("GetBatteryChargeMode"));
                readAny = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unavailable: " + RootMessage(ex));
            }
            Console.WriteLine();
            Console.WriteLine("== Performance management ==");
            try
            {
                PrintResponse(InvokeAgent("GetITSMode", PowerReadRequest()));
                readAny = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unavailable: " + RootMessage(ex));
            }
            return readAny ? 0 : 1;
        }

        private static int HandleCharge(string[] args)
        {
            if (args.Length == 2 && EqualsArg(args[1], "get"))
            {
                PrintResponse(InvokeAgent("GetBatteryChargeMode"));
                return 0;
            }

            if (args.Length >= 3 && EqualsArg(args[1], "set"))
            {
                BatteryChargeModeType mode;
                if (!ChargeModes.TryGetValue(args[2], out mode))
                    return InvalidMode("charge");
                RequireApply(args);

                Console.WriteLine("Before:");
                object before = InvokeAgent("GetBatteryChargeMode");
                PrintResponse(before);
                EnsureResponseSuccess(before, "读取充电能力");
                EnsureChargeSupported(before, mode);
                Console.WriteLine("Set response:");
                object response = InvokeAgent("SetBatteryChargeMode", new BatteryMgmtRequest
                {
                    BatteryChargeMode = mode
                });
                PrintResponse(response);
                EnsureResponseSuccess(response, "充电模式");
                Console.WriteLine("After:");
                PrintResponse(InvokeAgent("GetBatteryChargeMode"));
                return 0;
            }

            PrintUsage();
            return 2;
        }

        private static int HandlePerformance(string[] args)
        {
            if (args.Length == 2 && EqualsArg(args[1], "get"))
            {
                PrintResponse(InvokeAgent("GetITSMode", PowerReadRequest()));
                return 0;
            }

            if (args.Length >= 3 && EqualsArg(args[1], "set"))
            {
                ItsModeType mode;
                if (!PerformanceModes.TryGetValue(args[2], out mode))
                    return InvalidMode("performance");
                RequireApply(args);

                Console.WriteLine("Before:");
                object before = InvokeAgent("GetITSMode", PowerReadRequest());
                PrintResponse(before);
                EnsureResponseSuccess(before, "读取性能能力");
                EnsurePerformanceSupported(before, mode);
                Console.WriteLine("Set response:");
                object response = InvokeAgent("SetITSMode", new PowerSettingsRequest
                {
                    ItsMode = mode,
                    UISupportGeekMode = true
                });
                PrintResponse(response);
                EnsureResponseSuccess(response, "性能模式");
                Console.WriteLine("After:");
                PrintResponse(InvokeAgent("GetITSMode", PowerReadRequest()));
                return 0;
            }

            if (args.Length >= 3 && EqualsArg(args[1], "auto-transition"))
            {
                bool enabled;
                if (EqualsArg(args[2], "on")) enabled = true;
                else if (EqualsArg(args[2], "off")) enabled = false;
                else throw new ArgumentException("auto-transition must be on or off.");

                RequireApply(args);
                Console.WriteLine("Set response:");
                object response = InvokeAgent("SetITSAutoTransition", new PowerSettingsRequest
                {
                    IsAutoTransitionEnabled = enabled
                });
                PrintResponse(response);
                EnsureResponseSuccess(response, "自动切换");
                Console.WriteLine("After:");
                PrintResponse(InvokeAgent("GetITSMode", PowerReadRequest()));
                return 0;
            }

            PrintUsage();
            return 2;
        }

        private static PowerSettingsRequest PowerReadRequest()
        {
            return new PowerSettingsRequest
            {
                UISupportGeekMode = true
            };
        }

        private static object InvokeAgent(string methodName, params object[] arguments)
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

        private static void EnsureAgent()
        {
            if (agent != null) return;

            string assemblyPath = AddinLocator.FindAssembly();
            if (String.IsNullOrWhiteSpace(assemblyPath))
                throw new FileNotFoundException(
                    "未检测到 Lenovo Vantage/百应的 IdeaNotebookAddin。此电脑可能不是联想设备，或相关服务未安装。",
                    AddinAssemblyName);
            Assembly addin = Assembly.LoadFrom(assemblyPath);
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

        private static void PrintResponse(object response)
        {
            if (response == null)
            {
                Console.WriteLine("null");
                return;
            }

            string json = AddinResponse.ToJson(response);
            try
            {
                Console.WriteLine(JToken.Parse(json).ToString(Formatting.Indented));
            }
            catch (Exception)
            {
                Console.WriteLine(json);
            }
        }

        private static MethodInfo FindMethod(string methodName, object[] arguments)
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

        private static void EnsureChargeSupported(object response, BatteryChargeModeType mode)
        {
            string supported = AddinResponse.ReadSetting(
                response,
                "Supported-BatteryChargeMode",
                "SupportedBatteryChargeMode",
                "BatteryChargeModeSupported");
            string[] aliases;
            switch (mode)
            {
                case BatteryChargeModeType.Normal:
                    aliases = new[] { "Normal", "Standard", "Regular" };
                    break;
                case BatteryChargeModeType.Storage:
                    aliases = new[] { "Storage", "Conservation", "BatteryConservation", "LongLife" };
                    break;
                default:
                    aliases = new[] { "Quick", "Express", "Rapid", "RapidCharge" };
                    break;
            }
            if (!CapabilityNames.Matches(supported, aliases))
                throw new InvalidOperationException(
                    "设备未报告支持该充电模式（Supported-BatteryChargeMode=" +
                    (String.IsNullOrWhiteSpace(supported) ? "未报告" : supported) + "）。");
        }

        private static void EnsurePerformanceSupported(object response, ItsModeType mode)
        {
            string supported = AddinResponse.ReadSetting(
                response,
                "Supported-ITSMode",
                "SupportedITSMode",
                "ITSModeSupported");
            string[] aliases;
            switch (mode)
            {
                case ItsModeType.ItsAuto:
                    aliases = new[] { "ITS_Auto", "MMC_Auto", "MMC_Balance", "Auto", "Balance", "Balanced", "Smart", "IntelligentCooling" };
                    break;
                case ItsModeType.MmcCool:
                    aliases = new[] { "MMC_Cool", "MMC_Quiet", "Quiet", "Silent", "Cool", "Bsm_Quiet", "BatterySaving", "EnergySaving" };
                    break;
                case ItsModeType.MmcPerformance:
                    aliases = new[] { "MMC_Performance", "MMC_Extreme", "Performance", "Extreme", "Turbo" };
                    break;
                default:
                    aliases = new[] { "MMC_Geek", "Geek", "Creator", "Creative" };
                    break;
            }
            if (!CapabilityNames.Matches(supported, aliases))
                throw new InvalidOperationException(
                    "设备未报告支持该性能模式（Supported-ITSMode=" +
                    (String.IsNullOrWhiteSpace(supported) ? "未报告" : supported) + "）。");
        }

        private static void EnsureResponseSuccess(object response, string feature)
        {
            if (!AddinResponse.IsSuccess(response))
                throw new InvalidOperationException(
                    feature + "设置被设备拒绝（ErrorCode=" +
                    (AddinResponse.ErrorCode(response) ?? "未知") + "）。");
        }

        private static int ShowDiagnostics()
        {
            string path = AddinLocator.FindAssembly();
            Console.WriteLine("LenovoSettingsDemo diagnostics");
            Console.WriteLine("OS architecture: " + (Environment.Is64BitOperatingSystem ? "x64" : "x86"));
            Console.WriteLine("Process architecture: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            Console.WriteLine("Hardware: " + AddinLocator.HardwareSummary());
            Console.WriteLine("IdeaNotebookAddin: " + AddinLocator.DescribeAssembly(path));
            if (String.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("Result: unsupported (Lenovo Addin not installed)");
                return 1;
            }
            try
            {
                EnsureAgent();
                Console.WriteLine("Agent: " + agentType.FullName);
                Console.WriteLine("GetBatteryChargeMode: " + (FindMethod("GetBatteryChargeMode", new object[0]) != null ? "yes" : "no"));
                Console.WriteLine("GetITSMode: " + (FindMethod("GetITSMode", new object[] { PowerReadRequest() }) != null ? "yes" : "no"));
                Console.WriteLine("Result: Addin loaded; run status for device capabilities");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Result: load failed - " + RootMessage(ex));
                return 1;
            }
        }

        private static string RootMessage(Exception exception)
        {
            Exception cause = exception;
            while (cause.InnerException != null) cause = cause.InnerException;
            return cause.Message;
        }

        private static void RequireApply(string[] args)
        {
            foreach (string arg in args)
                if (EqualsArg(arg, "--apply")) return;
            throw new InvalidOperationException(
                "Write operation blocked. Re-run with --apply after checking the requested mode.");
        }

        private static int InvalidMode(string category)
        {
            Console.Error.WriteLine(
                "Unsupported " + category + " mode. Run with --help to see valid modes.");
            return 2;
        }

        private static bool EqualsArg(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
                "LenovoSettingsDemo - call IdeaNotebookAddin without starting Baiying");
            Console.WriteLine();
            Console.WriteLine("Read-only:");
            Console.WriteLine("  LenovoSettingsDemo.exe status");
            Console.WriteLine("  LenovoSettingsDemo.exe charge get");
            Console.WriteLine("  LenovoSettingsDemo.exe performance get");
            Console.WriteLine("  LenovoSettingsDemo.exe diagnose");
            Console.WriteLine();
            Console.WriteLine("Writes (explicit --apply required):");
            Console.WriteLine(
                "  LenovoSettingsDemo.exe charge set normal|conservation|express --apply");
            Console.WriteLine(
                "  LenovoSettingsDemo.exe performance set auto|quiet|performance|geek --apply");
            Console.WriteLine(
                "  LenovoSettingsDemo.exe performance auto-transition on|off --apply");
        }
    }
}
