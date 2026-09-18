using System;
using System.Collections.Generic;
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

    internal interface IEnergyDriverTransport : IDisposable
    {
        uint Execute(byte command);
    }

    internal static class EnergyDriverProtocol
    {
        internal const byte QueryCommand = 0xFF;
        internal const byte EnableStorageCommand = 0x03;
        internal const byte DisableStorageCommand = 0x05;
        internal const byte EnableQuickCommand = 0x07;
        internal const byte DisableQuickCommand = 0x08;
        internal const byte EnableStorage80Command = 0x0D;
        internal const byte DisableStorage80Command = 0x0F;

        internal static DirectChargeState Decode(uint flags)
        {
            return new DirectChargeState
            {
                RawFlags = flags,
                StorageEnabled = (flags & 0x00000020) != 0,
                Storage80Capable = (flags & 0x00004000) != 0,
                QuickEnabled = (flags & 0x00000004) != 0,
                QuickCapable = (flags & 0x00020000) != 0
            };
        }

        internal static IList<byte> BuildTransitionCommands(
            DirectChargeState before,
            DirectChargeMode requested)
        {
            if (before == null) throw new ArgumentNullException("before");
            if (requested == DirectChargeMode.Unknown)
                throw new ArgumentOutOfRangeException("requested");
            var commands = new List<byte>();
            switch (requested)
            {
                case DirectChargeMode.Storage:
                    if (!before.StorageEnabled)
                    {
                        commands.Add(EnableStorageCommand);
                        if (before.Storage80Capable) commands.Add(EnableStorage80Command);
                    }
                    if (before.QuickEnabled) commands.Add(DisableQuickCommand);
                    break;
                case DirectChargeMode.Quick:
                    if (!before.QuickCapable)
                        throw new InvalidOperationException("固件未报告快充能力。");
                    if (!before.QuickEnabled) commands.Add(EnableQuickCommand);
                    if (before.StorageEnabled)
                    {
                        commands.Add(DisableStorageCommand);
                        if (before.Storage80Capable) commands.Add(DisableStorage80Command);
                    }
                    break;
                default:
                    if (before.StorageEnabled)
                    {
                        commands.Add(DisableStorageCommand);
                        if (before.Storage80Capable) commands.Add(DisableStorage80Command);
                    }
                    if (before.QuickEnabled) commands.Add(DisableQuickCommand);
                    break;
            }
            return commands;
        }

        internal static bool IsKnownCommand(byte command)
        {
            return command == QueryCommand || command == EnableStorageCommand ||
                command == DisableStorageCommand || command == EnableQuickCommand ||
                command == DisableQuickCommand || command == EnableStorage80Command ||
                command == DisableStorage80Command;
        }
    }

    // Direct protocol access is intentionally separated from transport so tests
    // can validate command sequences with a simulated device.
    internal sealed class EnergyDriverChargeClient
    {
        private const string DevicePath = @"\\.\EnergyDrv";
        private const uint EnergyIoctl = 0x831020F8;
        private readonly Func<IEnergyDriverTransport> transportFactory;
        private readonly int pollAttempts;
        private readonly int pollDelayMilliseconds;

        public EnergyDriverChargeClient()
            : this(delegate { return new WindowsEnergyDriverTransport(); }, 10, 50)
        {
        }

        internal EnergyDriverChargeClient(
            Func<IEnergyDriverTransport> transportFactory,
            int pollAttempts,
            int pollDelayMilliseconds)
        {
            this.transportFactory = transportFactory ??
                throw new ArgumentNullException("transportFactory");
            this.pollAttempts = Math.Max(1, pollAttempts);
            this.pollDelayMilliseconds = Math.Max(0, pollDelayMilliseconds);
        }

        public DirectChargeState Read()
        {
            using (IEnergyDriverTransport transport = transportFactory())
                return Query(transport);
        }

        public DirectChargeState SetMode(DirectChargeMode requested)
        {
            using (IEnergyDriverTransport transport = transportFactory())
            {
                DirectChargeState before = Query(transport);
                foreach (byte command in EnergyDriverProtocol.BuildTransitionCommands(before, requested))
                    Send(transport, command);
                DirectChargeState after = before;
                for (int attempt = 0; attempt < pollAttempts; attempt++)
                {
                    after = Query(transport);
                    if (after.Mode == requested) return after;
                    if (pollDelayMilliseconds > 0) Thread.Sleep(pollDelayMilliseconds);
                }
                throw new InvalidOperationException(
                    "驱动接受了请求，但充电模式没有切换到 " + requested +
                    "；当前为 " + after.Mode + "。不同固件可能需要重新插拔电源后生效。");
            }
        }

        public static DirectChargeState DecodeFlags(uint flags)
        {
            return EnergyDriverProtocol.Decode(flags);
        }

        public static IList<byte> PlanCommands(DirectChargeState before, DirectChargeMode requested)
        {
            return EnergyDriverProtocol.BuildTransitionCommands(before, requested);
        }

        public static string DescribeProtocol()
        {
            return DevicePath + " / IOCTL 0x" + EnergyIoctl.ToString("X8");
        }

        private static DirectChargeState Query(IEnergyDriverTransport transport)
        {
            return EnergyDriverProtocol.Decode(Send(transport, EnergyDriverProtocol.QueryCommand));
        }

        private static uint Send(IEnergyDriverTransport transport, byte command)
        {
            if (!EnergyDriverProtocol.IsKnownCommand(command))
                throw new InvalidOperationException("拒绝未列入白名单的 EnergyDrv 命令。");
            return transport.Execute(command);
        }
    }

    internal sealed class WindowsEnergyDriverTransport : IEnergyDriverTransport
    {
        private const string DevicePath = @"\\.\EnergyDrv";
        private const uint EnergyIoctl = 0x831020F8;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileAttributeNormal = 0x80;
        private SafeFileHandle handle;

        public WindowsEnergyDriverTransport()
        {
            handle = CreateFile(
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
        }

        public uint Execute(byte command)
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
                throw new InvalidOperationException("EnergyDrv 返回了意外的数据长度：" + returned + "。");
            return output;
        }

        public void Dispose()
        {
            if (handle != null)
            {
                handle.Dispose();
                handle = null;
            }
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
