using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LenovoSettingsCompat
{
    internal sealed class ChargeThresholdState
    {
        public int Slot { get; set; }
        public bool IsCapable { get; set; }
        public bool IsEnabled { get; set; }
        public int StartValue { get; set; }
        public int StopValue { get; set; }
        public bool IsWritable { get; set; }
        public string ClientInfo { get; set; }
    }

    internal sealed class PowerRpcException : InvalidOperationException
    {
        public int ErrorCode { get; private set; }

        public PowerRpcException(string operation, int errorCode)
            : base(Describe(operation, errorCode))
        {
            ErrorCode = errorCode;
        }

        private static string Describe(string operation, int errorCode)
        {
            string detail;
            switch (errorCode)
            {
                case 1722:
                    detail = "Lenovo Power RPC 服务未运行，请先启动 Lenovo Vantage 或联想百应";
                    break;
                case 1775:
                    detail = "Lenovo Power RPC 会话不可用";
                    break;
                default:
                    detail = "设备返回错误";
                    break;
            }
            return operation + "失败：" + detail + "（错误码 " + errorCode + "）。";
        }
    }

    internal sealed class ChargeThresholdClient
    {
        private const string ClientAssemblyFileName = "Lenovo.Vantage.PowerRpcClient.dll";
        private const string ClientTypeName = "ThinkPowerClient.RpcClient";

        private object client;
        private Type clientType;
        private string assemblyPath;
        private bool initialized;

        public ChargeThresholdState Read(int slot)
        {
            ValidateSlot(slot);
            EnsureInitialized();
            MethodInfo method = FindMethod("ClientGetChargeThreshold", 5);
            object[] arguments = { slot, false, false, 0, 0 };
            int result = InvokeResult(method, arguments);
            EnsureSuccess("读取充电阈值", result);
            return new ChargeThresholdState
            {
                Slot = slot,
                IsCapable = Convert.ToBoolean(arguments[1]),
                IsEnabled = Convert.ToBoolean(arguments[2]),
                StartValue = Convert.ToInt32(arguments[3]),
                StopValue = Convert.ToInt32(arguments[4]),
                IsWritable = FindMethodOrNull("ClientSetChargeThreshold", 3) != null,
                ClientInfo = DescribeClient()
            };
        }

        public void Set(int slot, int startValue, int stopValue)
        {
            ValidateSlot(slot);
            ValidateValues(startValue, stopValue);
            EnsureInitialized();
            MethodInfo method = FindMethod("ClientSetChargeThreshold", 3);
            int result = InvokeResult(method, new object[] { slot, startValue, stopValue });
            EnsureSuccess("设置充电阈值", result);
        }

        public static void ValidateValues(int startValue, int stopValue)
        {
            if (startValue < 0 || startValue > 100)
                throw new ArgumentOutOfRangeException(
                    "startValue", "起充阈值必须在 0 到 100 之间。");
            if (stopValue < 1 || stopValue > 100)
                throw new ArgumentOutOfRangeException(
                    "stopValue", "停充阈值必须在 1 到 100 之间。");
            if (startValue >= stopValue)
                throw new ArgumentException("起充阈值必须小于停充阈值。");
        }

        public static string FindClientAssembly()
        {
            string overridePath = Environment.GetEnvironmentVariable(
                "LENOVO_POWER_RPC_PATH");
            if (!String.IsNullOrWhiteSpace(overridePath))
            {
                if (File.Exists(overridePath)) return overridePath;
                if (Directory.Exists(overridePath))
                {
                    string candidate = Path.Combine(overridePath, ClientAssemblyFileName);
                    if (File.Exists(candidate)) return candidate;
                }
                return null;
            }

            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ClientAssemblyFileName);
            if (File.Exists(local)) return local;

            string commonData = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            string[] addinRoots =
            {
                Path.Combine(commonData, "Lenovo", "Vantage", "Addins"),
                Path.Combine(commonData, "Lenovo", "Commercial Vantage", "Addins"),
                Path.Combine(commonData, "Lenovo", "Lenovo Vantage", "Addins")
            };
            return FindNewestClient(addinRoots);
        }

        public static string DescribeAvailability()
        {
            string path = FindClientAssembly();
            if (String.IsNullOrWhiteSpace(path)) return "未找到";
            try
            {
                AssemblyName name = System.Reflection.AssemblyName.GetAssemblyName(path);
                return Path.GetFileName(path) +
                    (name == null || name.Version == null ? "" : " " + name.Version);
            }
            catch
            {
                return Path.GetFileName(path);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            assemblyPath = FindClientAssembly();
            if (String.IsNullOrWhiteSpace(assemblyPath))
                throw new FileNotFoundException(
                    "未检测到 Lenovo Power RPC 客户端，无法读取自定义充电阈值。",
                    ClientAssemblyFileName);

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            clientType = assembly.GetType(ClientTypeName, false);
            if (clientType == null)
                throw new MissingMethodException(
                    "Lenovo Power RPC 客户端中不存在 " + ClientTypeName + "。");
            client = Activator.CreateInstance(clientType);
            if (client == null)
                throw new InvalidOperationException("无法创建 Lenovo Power RPC 客户端。");

            int result = InvokeResult(FindMethod("ClientInitialize", 0), new object[0]);
            EnsureSuccess("初始化充电阈值接口", result);
            initialized = true;
        }

        private MethodInfo FindMethod(string name, int parameterCount)
        {
            MethodInfo method = FindMethodOrNull(name, parameterCount);
            if (method == null)
                throw new MissingMethodException(ClientTypeName, name);
            return method;
        }

        private MethodInfo FindMethodOrNull(string name, int parameterCount)
        {
            if (clientType == null) return null;
            foreach (MethodInfo method in clientType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (String.Equals(method.Name, name, StringComparison.Ordinal) &&
                    method.GetParameters().Length == parameterCount)
                    return method;
            }
            return null;
        }

        private int InvokeResult(MethodInfo method, object[] arguments)
        {
            try
            {
                return Convert.ToInt32(method.Invoke(client, arguments));
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private string DescribeClient()
        {
            if (String.IsNullOrWhiteSpace(assemblyPath)) return "未知";
            try
            {
                Version version = AssemblyName.GetAssemblyName(assemblyPath).Version;
                return Path.GetFileName(assemblyPath) +
                    (version == null ? "" : " " + version);
            }
            catch
            {
                return Path.GetFileName(assemblyPath);
            }
        }

        private static void EnsureSuccess(string operation, int result)
        {
            if (result != 0) throw new PowerRpcException(operation, result);
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0)
                throw new ArgumentOutOfRangeException("slot", "电池槽位不能为负数。");
        }

        private static string FindNewestClient(string[] addinRoots)
        {
            var candidates = new List<string>();
            foreach (string root in addinRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (string path in Directory.GetFiles(
                        root, ClientAssemblyFileName, SearchOption.AllDirectories))
                        candidates.Add(path);
                }
                catch
                {
                    // Some enterprise installations deny access to unrelated add-ins.
                }
            }

            string best = null;
            Version bestVersion = null;
            foreach (string candidate in candidates)
            {
                Version version = null;
                try
                {
                    version = System.Reflection.AssemblyName.GetAssemblyName(candidate).Version;
                }
                catch
                {
                    // Keep malformed or inaccessible candidates out of selection.
                }
                if (version != null && (bestVersion == null || version > bestVersion))
                {
                    best = candidate;
                    bestVersion = version;
                }
            }
            return best;
        }
    }
}
