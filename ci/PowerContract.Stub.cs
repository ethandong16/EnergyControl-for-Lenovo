// Compile-time API surface only. This is not a Lenovo implementation or runtime replacement.
namespace Lenovo.Modern.Contracts.Power
{
    public enum ItsModeType
    {
        None = 0,
        ItsAuto = 1,
        MmcCool = 2,
        MmcPerformance = 3,
        MmcGeek = 4
    }

    public sealed class PowerSettingsRequest
    {
        public ItsModeType ItsMode { get; set; }
        public bool UISupportGeekMode { get; set; }
        public bool IsAutoTransitionEnabled { get; set; }
    }
}
