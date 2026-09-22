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
        public bool KeyboardBacklightRestoreWritable;
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
                state.ChargeBackend = UiText.Get("直接驱动");
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
                        state.ChargeError = UiText.Get("设备返回错误 ") + chargeErrorCode;
                    state.ChargeWritable = optionalClient.HasMethod("SetBatteryChargeMode");
                    state.ChargeBackend = "Lenovo Addin";
                }
                catch (Exception addinError)
                {
                    state.ChargeError = UiText.Get("直接驱动：") + RootMessage(directError) + UiText.Get("；Addin：") + RootMessage(addinError);
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
                    state.PerformanceError = UiText.Get("设备返回错误 ") + state.ErrorCode;
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
                state.KeyboardBacklightRestoreWritable = keyboardBacklightClient.HasMethod("KeyboardBacklightRestoreDefault");
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

}
