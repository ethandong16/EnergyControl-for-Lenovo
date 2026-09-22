using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace LenovoSettingsCompat
{
    internal enum KeyboardBacklightLevel
    {
        Off = 0,
        Level1 = 1,
        Level2 = 2,
        Auto = 3,
        DisabledOff = 4
    }

    internal static class KeyboardBacklightNames
    {
        public static string ToContractValue(KeyboardBacklightLevel level)
        {
            switch (level)
            {
                case KeyboardBacklightLevel.Level1: return "Level_1";
                case KeyboardBacklightLevel.Level2: return "Level_2";
                case KeyboardBacklightLevel.Auto: return "Auto";
                case KeyboardBacklightLevel.DisabledOff: return "DisabledOff";
                default: return "Off";
            }
        }

        public static string DisplayName(string value)
        {
            if (String.Equals(value, "Level_1", StringComparison.OrdinalIgnoreCase)) return UiText.Get("一级亮度");
            if (String.Equals(value, "Level_2", StringComparison.OrdinalIgnoreCase)) return UiText.Get("二级亮度");
            if (String.Equals(value, "DisabledOff", StringComparison.OrdinalIgnoreCase)) return UiText.Get("关闭（禁用）");
            if (String.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase)) return UiText.Get("自动");
            if (String.Equals(value, "Off", StringComparison.OrdinalIgnoreCase)) return UiText.Get("关闭");
            return String.IsNullOrWhiteSpace(value) ? UiText.Get("未知") : value;
        }

        public static bool TryParse(string value, out KeyboardBacklightLevel level)
        {
            if (String.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "disabled-off", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "disabledoff", StringComparison.OrdinalIgnoreCase))
            {
                level = String.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
                    ? KeyboardBacklightLevel.Off
                    : KeyboardBacklightLevel.DisabledOff;
                return true;
            }
            if (String.Equals(value, "level1", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "level_1", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "one", StringComparison.OrdinalIgnoreCase))
            {
                level = KeyboardBacklightLevel.Level1;
                return true;
            }
            if (String.Equals(value, "level2", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "level_2", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "two", StringComparison.OrdinalIgnoreCase))
            {
                level = KeyboardBacklightLevel.Level2;
                return true;
            }
            if (String.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
            {
                level = KeyboardBacklightLevel.Auto;
                return true;
            }
            level = KeyboardBacklightLevel.Off;
            return false;
        }
    }

    internal sealed class KeyboardBacklightState
    {
        public bool IsSupported { get; internal set; }
        public bool IsWritable { get; internal set; }
        public bool CanReserve { get; internal set; }
        public bool CanAutoDim { get; internal set; }
        public string Status { get; internal set; }
        public string LevelCapability { get; internal set; }
        public string Reserve { get; internal set; }
        public string AutoDimCapability { get; internal set; }
        public string AutoDimStatus { get; internal set; }
        public string Timeout { get; internal set; }
        public string Error { get; internal set; }
        public string AgentInfo { get; internal set; }
        public IDictionary<string, string> Capabilities { get; internal set; }
        public IDictionary<string, string> Settings { get; internal set; }
    }

    internal static class KeyboardBacklightResponse
    {
        public static IDictionary<string, string> ReadSettings(object response)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (object item in ReadItems(response))
            {
                string key = Convert.ToString(GetMember(item, "key", "Key"), CultureInfo.InvariantCulture);
                if (String.IsNullOrWhiteSpace(key)) continue;
                object value = GetMember(item, "value", "Value");
                values[key] = value == null
                    ? null
                    : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            return values;
        }

        public static bool IsSuccess(object response)
        {
            if (response == null) return true;
            string directError = ReadErrorValue(response);
            if (!String.IsNullOrWhiteSpace(directError) && !IsSuccessValue(directError))
                return false;
            foreach (object item in ReadItems(response))
            {
                string error = Convert.ToString(
                    GetMember(item, "errorCode", "ErrorCode"),
                    CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(error) && !IsSuccessValue(error))
                    return false;
            }
            return true;
        }

        public static string ErrorCode(object response)
        {
            string directError = ReadErrorValue(response);
            if (!String.IsNullOrWhiteSpace(directError) && !IsSuccessValue(directError))
                return directError;
            foreach (object item in ReadItems(response))
            {
                string error = Convert.ToString(
                    GetMember(item, "errorCode", "ErrorCode"),
                    CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(error) && !IsSuccessValue(error))
                    return error;
            }
            return String.IsNullOrWhiteSpace(directError) ? "0" : directError;
        }

        internal static IEnumerable<object> ReadItems(object response)
        {
            if (response == null) yield break;
            object list = GetMember(response, "List", "SettingList", "settingList");
            object items = GetMember(list, "Items", "items");
            if (items == null) items = GetMember(response, "Items", "items");
            IEnumerable enumerable = items as IEnumerable;
            if (enumerable == null || items is string) yield break;
            foreach (object item in enumerable) yield return item;
        }

        internal static object GetMember(object source, params string[] names)
        {
            if (source == null || names == null) return null;
            IDictionary dictionary = source as IDictionary;
            if (dictionary != null)
            {
                foreach (object rawKey in dictionary.Keys)
                {
                    string key = Convert.ToString(rawKey, CultureInfo.InvariantCulture);
                    foreach (string name in names)
                        if (String.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                            return dictionary[rawKey];
                }
            }
            Type type = source.GetType();
            foreach (string name in names)
            {
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (String.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                        property.CanRead)
                    {
                        try { return property.GetValue(source, null); }
                        catch { return null; }
                    }
                }
                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (String.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        try { return field.GetValue(source); }
                        catch { return null; }
                    }
                }
            }
            return null;
        }

        private static string ReadErrorValue(object response)
        {
            object error = GetMember(response, "ErrorCode", "ErrorCodeValue", "errorcode");
            return error == null ? null : Convert.ToString(error, CultureInfo.InvariantCulture);
        }

        private static bool IsSuccessValue(string value)
        {
            return String.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "Success", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "OK", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class LenovoKeyboardBacklightClient
    {
        private const string AddinAssemblyName = "IdeaNotebookAddin.dll";
        private const string AgentTypeName = "IdeaNotebookAddin.IdeaNotebookAgent";
        private object agent;
        private Type agentType;
        private string assemblyPath;

        public KeyboardBacklightState Read()
        {
            EnsureAgent();
            var state = new KeyboardBacklightState
            {
                AgentInfo = AddinLocator.DescribeAssembly(assemblyPath),
                Capabilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                IsWritable = HasMethod("SetBacklightStatus"),
                CanReserve = HasMethod("SetBacklightReserve"),
                CanAutoDim = HasMethod("SetBacklightAutoDim")
            };

            bool reportedSupport = false;
            try { reportedSupport = Convert.ToBoolean(Invoke("IsSupportBacklight")); }
            catch (Exception ex) { state.Error = RootMessage(ex); }

            try
            {
                state.Capabilities = new Dictionary<string, string>(
                    KeyboardBacklightResponse.ReadSettings(Invoke("GetCapabilityResponse")),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                if (String.IsNullOrWhiteSpace(state.Error)) state.Error = RootMessage(ex);
            }

            try
            {
                object statusResponse = HasMethod("GetBacklightStatus")
                    ? Invoke("GetBacklightStatus")
                    : Invoke("GetKeyboardSettings");
                state.Settings = new Dictionary<string, string>(
                    KeyboardBacklightResponse.ReadSettings(statusResponse),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                if (String.IsNullOrWhiteSpace(state.Error)) state.Error = RootMessage(ex);
            }

            state.IsSupported = reportedSupport ||
                IsTrue(state.Capabilities, "KeyboardBacklightControl") ||
                state.Settings.ContainsKey("KeyboardBacklightStatus");
            state.Status = Read(state.Settings, "KeyboardBacklightStatus");
            state.LevelCapability = Read(state.Settings, "KeyboardBacklightLevel");
            state.Reserve = Read(state.Settings, "KeyboardBacklightReserve");
            state.AutoDimCapability = Read(state.Settings, "KeyboardBacklightAutoDimCapability");
            state.AutoDimStatus = Read(state.Settings, "KeyboardBacklightAutoDimStatus");
            state.Timeout = Read(state.Settings, "KeyboardBacklightTimeOut");
            state.CanAutoDim = state.CanAutoDim &&
                String.Equals(state.AutoDimCapability, "True", StringComparison.OrdinalIgnoreCase);
            if (!state.IsSupported && String.IsNullOrWhiteSpace(state.Error))
                state.Error = UiText.Get("设备未报告键盘背光能力。");
            return state;
        }

        public object SetLevel(KeyboardBacklightLevel level)
        {
            return Invoke("SetBacklightStatus", CreateStatusRequest(level));
        }

        public object SetReserve(bool enabled)
        {
            return Invoke("SetBacklightReserve", enabled);
        }

        public object SetAutoDim(bool enabled)
        {
            return Invoke("SetBacklightAutoDim", enabled);
        }

        public object RestoreDefault()
        {
            return Invoke("KeyboardBacklightRestoreDefault");
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
                return agentType == null ? UiText.Get("未知") : agentType.FullName;
            }
        }

        private object CreateStatusRequest(KeyboardBacklightLevel level)
        {
            EnsureAgent();
            MethodInfo method = FindMethod("SetBacklightStatus", new object[] { null });
            if (method == null)
                throw new MissingMethodException(agentType.FullName, "SetBacklightStatus");
            Type requestType = method.GetParameters()[0].ParameterType;
            object request = Activator.CreateInstance(requestType);
            object setting = CreateSetting(request, KeyboardBacklightNames.ToContractValue(level));
            if (setting == null)
                throw new InvalidOperationException(UiText.Get("无法构造 KeyboardSettingsRequest 请求体。"));
            return request;
        }

        private object CreateSetting(object request, string value)
        {
            object list = GetMember(request, "List", "SettingList", "settingList");
            if (list == null)
            {
                Type listType = FindMemberType(request.GetType(), "List", "SettingList", "settingList");
                if (listType == null) return null;
                list = Activator.CreateInstance(listType);
                if (!SetMember(request, list, "List", "SettingList", "settingList")) return null;
            }

            Type itemType = FindItemType(list.GetType());
            if (itemType == null)
                itemType = FindContractType("Lenovo.Modern.Contracts.Keyboard.Setting");
            if (itemType == null) return null;
            object setting = Activator.CreateInstance(itemType);
            if (!SetMember(setting, "KeyboardBacklightStatus", "key", "Key")) return null;
            if (!SetMember(setting, value, "value", "Value")) return null;

            object items = GetMember(list, "Items", "items");
            if (items == null)
            {
                Type itemsType = FindMemberType(list.GetType(), "Items", "items");
                if (itemsType == null) return null;
                items = CreateCollection(itemsType, itemType);
                if (!SetMember(list, items, "Items", "items")) return null;
            }
            IList collection = items as IList;
            if (collection == null) return null;
            collection.Add(setting);
            // KeyboardContract exposes List as a projection; assign the
            // populated object back so its internal JSON list is retained.
            if (!SetMember(request, list, "List", "SettingList", "settingList")) return null;
            return setting;
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
            assemblyPath = AddinLocator.FindAssembly();
            if (String.IsNullOrWhiteSpace(assemblyPath))
                throw new FileNotFoundException(
                    UiText.Get("未检测到 Lenovo Vantage/百应的 IdeaNotebookAddin。此电脑可能不是联想设备，或相关服务未安装。"),
                    AddinAssemblyName);
            Assembly addin = Assembly.LoadFrom(assemblyPath);
            agentType = AddinLocator.FindAgentType(addin);
            if (agentType == null)
                throw new MissingMethodException(UiText.Get("未找到兼容的 Lenovo 设备代理类型。"));
            MethodInfo getInstance = agentType.GetMethod(
                "GetInstance", BindingFlags.Public | BindingFlags.Static);
            if (getInstance == null)
                throw new MissingMethodException(AgentTypeName, "GetInstance");
            agent = getInstance.Invoke(null, null);
            if (agent == null) throw new InvalidOperationException("IdeaNotebookAgent.GetInstance() returned null.");
        }

        private MethodInfo FindMethod(string methodName, object[] arguments)
        {
            if (agentType == null) return null;
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

        private static object GetMember(object source, params string[] names)
        {
            return KeyboardBacklightResponse.GetMember(source, names);
        }

        private static string Read(IDictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value) ? value : null;
        }

        private static bool IsTrue(IDictionary<string, string> values, string key)
        {
            return String.Equals(Read(values, key), "True", StringComparison.OrdinalIgnoreCase);
        }

        private static Type FindMemberType(Type type, params string[] names)
        {
            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
                foreach (string name in names)
                    if (String.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        return property.PropertyType;
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Public | BindingFlags.Instance))
                foreach (string name in names)
                    if (String.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                        return field.FieldType;
            return null;
        }

        private static bool SetMember(object target, object value, params string[] names)
        {
            if (target == null) return false;
            Type type = target.GetType();
            foreach (string name in names)
            {
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!String.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        !property.CanWrite) continue;
                    property.SetValue(target, ConvertValue(value, property.PropertyType), null);
                    return true;
                }
                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!String.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    field.SetValue(target, ConvertValue(value, field.FieldType));
                    return true;
                }
            }
            return false;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;
            Type effective = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effective == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (effective.IsEnum) return Enum.Parse(effective, Convert.ToString(value, CultureInfo.InvariantCulture), true);
            return Convert.ChangeType(value, effective, CultureInfo.InvariantCulture);
        }

        private static Type FindItemType(Type listType)
        {
            if (listType.IsGenericType)
            {
                Type[] arguments = listType.GetGenericArguments();
                if (arguments.Length == 1) return arguments[0];
            }
            return null;
        }

        private static object CreateCollection(Type declaredType, Type itemType)
        {
            if (declaredType.IsArray)
            {
                Array array = Array.CreateInstance(itemType, 1);
                return array;
            }
            if (!declaredType.IsInterface && !declaredType.IsAbstract)
                return Activator.CreateInstance(declaredType);
            Type listType = typeof(List<>).MakeGenericType(itemType);
            return Activator.CreateInstance(listType);
        }

        private static Type FindContractType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }

        private static string RootMessage(Exception exception)
        {
            Exception cause = exception;
            while (cause.InnerException != null) cause = cause.InnerException;
            return cause.Message;
        }
    }
}
