// Compile-time API surface only. This is not a Lenovo implementation or runtime replacement.
namespace Lenovo.Modern.Contracts.BatteryManagement
{
    public enum BatteryChargeModeType
    {
        Normal = 0,
        Storage = 1,
        Quick = 2
    }

    public sealed class BatteryMgmtRequest
    {
        public BatteryChargeModeType BatteryChargeMode { get; set; }
    }
}
