using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LenovoSettingsCompat
{
    internal static class AddinLocator
    {
        private const string AddinAssemblyName = "IdeaNotebookAddin.dll";

        public static string FindAssembly()
        {
            string overridePath = Environment.GetEnvironmentVariable(
                "LENOVO_SETTINGS_ADDIN_PATH");
            if (!String.IsNullOrWhiteSpace(overridePath))
            {
                if (File.Exists(overridePath)) return overridePath;
                if (Directory.Exists(overridePath))
                    return FindCandidateAssembly(overridePath);
                return null;
            }
            string localDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string local = FindCandidateAssembly(localDirectory);
            if (!String.IsNullOrWhiteSpace(local)) return local;

            string commonData = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            string[] roots =
            {
                Path.Combine(commonData, "Lenovo", "Vantage", "Addins", "IdeaNotebookAddin"),
                Path.Combine(commonData, "Lenovo", "Commercial Vantage", "Addins", "IdeaNotebookAddin"),
                Path.Combine(commonData, "Lenovo", "Lenovo Vantage", "Addins", "IdeaNotebookAddin")
            };
            return FindHighestVersion(roots);
        }

        public static Type FindAgentType(Assembly addin)
        {
            if (addin == null) return null;
            Type preferred = addin.GetType(
                "IdeaNotebookAddin.IdeaNotebookAgent", false);
            if (IsAgentType(preferred)) return preferred;
            Type[] types;
            try
            {
                types = addin.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            foreach (Type candidate in types)
                if (IsAgentType(candidate)) return candidate;
            return null;
        }

        public static string DescribeAssembly(string assemblyPath)
        {
            if (String.IsNullOrWhiteSpace(assemblyPath)) return "未找到";
            try
            {
                AssemblyName name = AssemblyName.GetAssemblyName(assemblyPath);
                return name.Version == null
                    ? Path.GetFileName(assemblyPath)
                    : Path.GetFileName(assemblyPath) + " " + name.Version;
            }
            catch
            {
                return Path.GetFileName(assemblyPath);
            }
        }

        public static string HardwareSummary()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer,Model,SystemFamily FROM Win32_ComputerSystem"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        string manufacturer = Convert.ToString(result["Manufacturer"]);
                        string model = Convert.ToString(result["Model"]);
                        string family = Convert.ToString(result["SystemFamily"]);
                        string identity = String.Join(" ", new[] { manufacturer, model, family });
                        if (!String.IsNullOrWhiteSpace(identity)) return identity.Trim();
                    }
                }
            }
            catch
            {
                // WMI can be disabled or denied by enterprise policy.
            }
            return "未知（WMI 不可用）";
        }

        private static string FindHighestVersion(string[] roots)
        {
            Version bestVersion = null;
            string bestPath = null;
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string directory in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(directory);
                    Version version;
                    if (!Version.TryParse(name, out version)) continue;
                    string candidate = FindCandidateAssembly(directory);
                    if (String.IsNullOrWhiteSpace(candidate)) continue;
                    if (bestVersion == null || version > bestVersion)
                    {
                        bestVersion = version;
                        bestPath = candidate;
                    }
                }
            }
            return bestPath;
        }

        private static string FindCandidateAssembly(string directory)
        {
            string preferred = Path.Combine(directory, AddinAssemblyName);
            if (File.Exists(preferred)) return preferred;
            if (!Directory.Exists(directory)) return null;
            foreach (string candidate in Directory.GetFiles(directory, "*Addin.dll"))
            {
                string name = Path.GetFileName(candidate);
                if (String.Equals(name, "Lenovo.Vantage.AddinInterface.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (name.IndexOf("Notebook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ThinkPad", StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            }
            foreach (string candidate in Directory.GetFiles(directory, "*Addin.dll"))
            {
                if (!String.Equals(
                    Path.GetFileName(candidate),
                    "Lenovo.Vantage.AddinInterface.dll",
                    StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static bool IsAgentType(Type candidate)
        {
            if (candidate == null) return false;
            if (candidate.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static) == null)
                return false;
            return candidate.GetMethod("GetBatteryChargeMode", BindingFlags.Public | BindingFlags.Instance) != null ||
                candidate.GetMethod("GetITSMode", BindingFlags.Public | BindingFlags.Instance) != null;
        }
    }

    internal static class AddinResponse
    {
        public static string ReadSetting(object response, params string[] keys)
        {
            if (response == null || keys == null || keys.Length == 0) return null;
            JObject root = ToObject(response);
            JArray settings = root["settingList"] as JArray;
            if (settings == null) return null;
            foreach (JToken setting in settings)
            {
                string key = setting["key"] == null ? null : (string)setting["key"];
                foreach (string expected in keys)
                {
                    if (String.Equals(key, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        JToken value = setting["value"];
                        return value == null || value.Type == JTokenType.Null
                            ? null
                            : value.Type == JTokenType.String
                                ? (string)value
                                : value.ToString(Formatting.None);
                    }
                }
            }
            return null;
        }

        public static string ErrorCode(object response)
        {
            return ReadSetting(response, "ErrorCode", "ErrorCodeValue", "errorcode");
        }

        public static bool IsSuccess(object response)
        {
            string error = ErrorCode(response);
            return String.IsNullOrWhiteSpace(error) ||
                String.Equals(error, "0", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(error, "OK", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(error, "Success", StringComparison.OrdinalIgnoreCase);
        }

        public static string ToJson(object response)
        {
            if (response == null) return "null";
            MethodInfo toJson = response.GetType().GetMethod(
                "ToJson",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            return toJson == null
                ? JsonConvert.SerializeObject(response)
                : (string)toJson.Invoke(response, null);
        }

        public static JObject ToObject(object response)
        {
            string json = ToJson(response);
            if (String.IsNullOrWhiteSpace(json) || String.Equals(json, "null", StringComparison.OrdinalIgnoreCase))
                return new JObject();
            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException)
            {
                return new JObject();
            }
        }
    }

    internal static class CapabilityNames
    {
        public static bool Matches(string supported, params string[] names)
        {
            if (String.IsNullOrWhiteSpace(supported)) return false;
            foreach (string value in Split(supported))
                foreach (string name in names)
                    if (String.Equals(Normalize(value), Normalize(name), StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        public static List<string> Split(string supported)
        {
            var values = new List<string>();
            if (String.IsNullOrWhiteSpace(supported)) return values;
            char[] separators = { ',', ';', '|', '/', '\\' };
            foreach (string value in supported.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = value.Trim();
                if (trimmed.Length > 0) values.Add(trimmed);
            }
            return values;
        }

        public static string Normalize(string value)
        {
            if (value == null) return String.Empty;
            var chars = new List<char>();
            foreach (char c in value.Trim())
                if (Char.IsLetterOrDigit(c)) chars.Add(Char.ToLowerInvariant(c));
            return new String(chars.ToArray());
        }
    }
}
