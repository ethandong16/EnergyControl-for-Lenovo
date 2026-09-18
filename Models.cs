using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace LenovoSettingsCompat
{
    internal enum ChargeMode
    {
        Normal = 0,
        Storage = 1,
        Quick = 2
    }

    internal enum PerformanceMode
    {
        Auto = 1,
        Cool = 2,
        Performance = 3,
        Geek = 4
    }

    internal sealed class LenovoOptionalFeaturesClient
    {
        private const string AddinAssemblyName = "IdeaNotebookAddin.dll";
        private const string AgentTypeName = "IdeaNotebookAddin.IdeaNotebookAgent";

        private object agent;
        private Type agentType;

        public object ReadCharge()
        {
            return Invoke("GetBatteryChargeMode");
        }

        public object SetCharge(ChargeMode mode)
        {
            object request = CreateRequest("SetBatteryChargeMode", new Dictionary<string, object>
            {
                { "BatteryChargeMode", mode }
            });
            return Invoke("SetBatteryChargeMode", request);
        }

        public object ReadPerformance()
        {
            return Invoke("GetITSMode", CreateRequest("GetITSMode", new Dictionary<string, object>
            {
                { "UISupportGeekMode", true }
            }));
        }

        public object SetPerformance(PerformanceMode mode)
        {
            object request = CreateRequest("SetITSMode", new Dictionary<string, object>
            {
                { "ItsMode", mode },
                { "UISupportGeekMode", true }
            });
            return Invoke("SetITSMode", request);
        }

        public object SetAutoTransition(bool enabled)
        {
            object request = CreateRequest("SetITSAutoTransition", new Dictionary<string, object>
            {
                { "IsAutoTransitionEnabled", enabled }
            });
            return Invoke("SetITSAutoTransition", request);
        }

        public bool HasMethod(string name)
        {
            EnsureAgent();
            return FindMethod(name, null) != null;
        }

        public string AgentDescription
        {
            get
            {
                EnsureAgent();
                return agentType == null ? "未知" : agentType.FullName;
            }
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
                throw new System.IO.FileNotFoundException(
                    "未检测到 Lenovo Vantage/百应的 IdeaNotebookAddin。此电脑可能不是联想设备，或相关服务未安装。",
                    AddinAssemblyName);
            Assembly addin = Assembly.LoadFrom(path);
            agentType = AddinLocator.FindAgentType(addin);
            if (agentType == null)
                throw new MissingMethodException(
                    "未在 Addin 中找到兼容的设备代理类型。可能是商用 Vantage 或不匹配的版本。");
            MethodInfo getInstance = agentType.GetMethod(
                "GetInstance", BindingFlags.Public | BindingFlags.Static);
            if (getInstance == null)
                throw new MissingMethodException(AgentTypeName, "GetInstance");
            agent = getInstance.Invoke(null, null);
            if (agent == null)
                throw new InvalidOperationException("IdeaNotebookAgent.GetInstance() returned null.");
        }

        private object CreateRequest(string methodName, IDictionary<string, object> values)
        {
            EnsureAgent();
            MethodInfo method = FindMethod(methodName, new object[] { null });
            if (method == null)
                throw new MissingMethodException(agentType.FullName, methodName);
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1)
                throw new MissingMethodException(agentType.FullName, methodName);
            object request = Activator.CreateInstance(parameters[0].ParameterType);
            foreach (KeyValuePair<string, object> item in values)
            {
                PropertyInfo property = parameters[0].ParameterType.GetProperty(
                    item.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null || !property.CanWrite) continue;
                property.SetValue(request, ConvertValue(item.Value, property.PropertyType), null);
            }
            return request;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            Type nullableType = Nullable.GetUnderlyingType(targetType);
            Type effectiveType = nullableType ?? targetType;
            if (effectiveType.IsEnum)
            {
                string enumName = MapEnumName(value);
                try { return Enum.Parse(effectiveType, enumName, true); }
                catch { return Enum.ToObject(effectiveType, Convert.ToInt32(value, CultureInfo.InvariantCulture)); }
            }
            if (targetType.IsInstanceOfType(value)) return value;
            return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }

        private static string MapEnumName(object value)
        {
            ChargeMode charge;
            if (value is ChargeMode)
            {
                charge = (ChargeMode)value;
                switch (charge)
                {
                    case ChargeMode.Storage: return "Storage";
                    case ChargeMode.Quick: return "Quick";
                    default: return "Normal";
                }
            }
            if (value is PerformanceMode)
            {
                switch ((PerformanceMode)value)
                {
                    case PerformanceMode.Cool: return "MmcCool";
                    case PerformanceMode.Performance: return "MmcPerformance";
                    case PerformanceMode.Geek: return "MmcGeek";
                    default: return "ItsAuto";
                }
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private MethodInfo FindMethod(string methodName, object[] arguments)
        {
            foreach (MethodInfo candidate in agentType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (!String.Equals(candidate.Name, methodName, StringComparison.Ordinal) ||
                    (arguments != null && candidate.GetParameters().Length != arguments.Length))
                    continue;
                if (arguments == null) return candidate;
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
    }
}
