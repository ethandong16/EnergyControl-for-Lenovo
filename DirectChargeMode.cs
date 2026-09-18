using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace LenovoSettingsCompat
{
    internal enum DirectChargeMode
    {
        Unknown = -1,
        Normal = 0,
        Storage = 1,
        Quick = 2
    }

    internal sealed class DirectChargeState
    {
        public uint RawFlags { get; internal set; }
        public bool StorageEnabled { get; internal set; }
        public bool Storage80Capable { get; internal set; }
        public bool QuickEnabled { get; internal set; }
        public bool QuickCapable { get; internal set; }

        public DirectChargeMode Mode
        {
            get
            {
                if (StorageEnabled) return DirectChargeMode.Storage;
                if (QuickEnabled) return DirectChargeMode.Quick;
                return DirectChargeMode.Normal;
            }
        }

        public string SupportedModes
        {
            get { return QuickCapable ? "Normal,Storage,Quick" : "Normal,Storage"; }
        }

        public string LimitDescription
        {
            get
            {
                return Storage80Capable
                    ? "固件固定 80%"
                    : "固件预设（未报告固定 80% 能力）";
            }
        }
    }

    // Reconstructed from Lenovo PowerBattery.dll. This talks to the Lenovo
    // ACPIVPC/EnergyDrv device directly; no Vantage or Baiying process/RPC is used.
    internal sealed class EnergyDriverChargeClient
    {
        private const string DevicePath = @"\\.\EnergyDrv";
        private const uint EnergyIoctl = 0x831020F8;
        private const byte QueryCommand = 0xFF;
        private const byte EnableStorageCommand = 0x03;
        private const byte DisableStorageCommand = 0x05;
        private const byte EnableQuickCommand = 0x07;
        private const byte DisableQuickCommand = 0x08;
        private const byte EnableStorage80Command = 0x0D;
        private const byte DisableStorage80Command = 0x0F;

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileAttributeNormal = 0x80;

        public DirectChargeState Read()
        {
            using (SafeFileHandle handle = OpenDevice())
                return Query(handle);
        }

        public DirectChargeState SetMode(DirectChargeMode requested)
        {
            if (requested == DirectChargeMode.Unknown)
                throw new ArgumentOutOfRangeException("requested");

            using (SafeFileHandle handle = OpenDevice())
            {
                DirectChargeState before = Query(handle);
                switch (requested)
                {
                    case DirectChargeMode.Storage:
                        if (!before.StorageEnabled)
                        {
                            Send(handle, EnableStorageCommand);
                            if (before.Storage80Capable)
                                Send(handle, EnableStorage80Command);
                        }
                        if (before.QuickEnabled)
                            Send(handle, DisableQuickCommand);
                        break;

                    case DirectChargeMode.Quick:
                        if (!before.QuickCapable)
                            throw new InvalidOperationException("固件未报告快充能力。");
                        if (!before.QuickEnabled)
                            Send(handle, EnableQuickCommand);
                        if (before.StorageEnabled)
                        {
                            Send(handle, DisableStorageCommand);
                            if (before.Storage80Capable)
                                Send(handle, DisableStorage80Command);
                        }
                        break;

                    default:
                        if (before.StorageEnabled)
                        {
                            Send(handle, DisableStorageCommand);
                            if (before.Storage80Capable)
                                Send(handle, DisableStorage80Command);
                        }
                        if (before.QuickEnabled)
                            Send(handle, DisableQuickCommand);
                        break;
                }

                DirectChargeState after = before;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    after = Query(handle);
                    if (after.Mode == requested) return after;
                    Thread.Sleep(50);
                }
                throw new InvalidOperationException(
                    "驱动接受了请求，但充电模式没有切换到 " + requested +
                    "；当前为 " + after.Mode + "。");
            }
        }

        public static string DescribeProtocol()
        {
            return DevicePath + " / IOCTL 0x" + EnergyIoctl.ToString("X8");
        }

        private static DirectChargeState Query(SafeFileHandle handle)
        {
            uint flags = Send(handle, QueryCommand);
            return new DirectChargeState
            {
                RawFlags = flags,
                StorageEnabled = (flags & 0x00000020) != 0,
                Storage80Capable = (flags & 0x00004000) != 0,
                QuickEnabled = (flags & 0x00000004) != 0,
                QuickCapable = (flags & 0x00020000) != 0
            };
        }

        private static uint Send(SafeFileHandle handle, byte command)
        {
            uint output;
            uint returned;
            byte[] input = { command };
            if (!DeviceIoControl(
                handle,
                EnergyIoctl,
                input,
                1,
                out output,
                4,
                out returned,
                IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "EnergyDrv 命令 0x" + command.ToString("X2") + " 失败");
            }
            if (returned != 4)
                throw new InvalidOperationException(
                    "EnergyDrv 返回了意外的数据长度：" + returned + "。");
            return output;
        }

        private static SafeFileHandle OpenDevice()
        {
            SafeFileHandle handle = CreateFile(
                DevicePath,
                GenericRead | GenericWrite,
                ShareRead | ShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(
                    error,
                    "无法打开 Lenovo EnergyDrv 设备；请确认 ACPIVPC 驱动已安装");
            }
            return handle;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint controlCode,
            byte[] input,
            uint inputSize,
            out uint output,
            uint outputSize,
            out uint bytesReturned,
            IntPtr overlapped);
    }
}
