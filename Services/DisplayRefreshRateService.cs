using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ImvixPro.Services
{
    public sealed class DisplayRefreshRateService
    {
        private const double DefaultRefreshRate = 60d;
        private const double MinimumRefreshRate = 30d;
        private const double MaximumRefreshRate = 360d;
        private const int EnumCurrentSettings = -1;
        private const int CchDeviceName = 32;
        private const int CchFormName = 32;

        public double GetRefreshRate(Screen? screen)
        {
            if (screen is null)
            {
                return DefaultRefreshRate;
            }

            var refreshRate = OperatingSystem.IsWindows()
                ? TryGetWindowsRefreshRate(screen)
                : null;

            return NormalizeRefreshRate(refreshRate ?? DefaultRefreshRate);
        }

        private static double NormalizeRefreshRate(double refreshRate)
        {
            if (double.IsNaN(refreshRate) || double.IsInfinity(refreshRate))
            {
                return DefaultRefreshRate;
            }

            return Math.Clamp(refreshRate, MinimumRefreshRate, MaximumRefreshRate);
        }

        [SupportedOSPlatform("windows")]
        private static double? TryGetWindowsRefreshRate(Screen screen)
        {
            var handle = screen.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero)
            {
                return null;
            }

            var monitorInfo = MonitorInfoEx.Create();
            if (!GetMonitorInfo(handle.Handle, ref monitorInfo))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(monitorInfo.DeviceName))
            {
                return null;
            }

            var devMode = DeviceMode.Create();
            if (!EnumDisplaySettings(monitorInfo.DeviceName, EnumCurrentSettings, ref devMode))
            {
                return null;
            }

            return devMode.DisplayFrequency > 1
                ? devMode.DisplayFrequency
                : null;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DeviceMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfoEx
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)]
            public string DeviceName;

            public static MonitorInfoEx Create()
            {
                return new MonitorInfoEx
                {
                    Size = Marshal.SizeOf<MonitorInfoEx>(),
                    DeviceName = string.Empty
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DeviceMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)]
            public string DeviceName;

            public short SpecVersion;
            public short DriverVersion;
            public short Size;
            public short DriverExtra;
            public int Fields;
            public NativePoint Position;
            public int DisplayOrientation;
            public int DisplayFixedOutput;
            public short Color;
            public short Duplex;
            public short YResolution;
            public short TTOption;
            public short Collate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
            public string FormName;

            public short LogPixels;
            public int BitsPerPel;
            public int PelsWidth;
            public int PelsHeight;
            public int DisplayFlags;
            public int DisplayFrequency;
            public int IcmMethod;
            public int IcmIntent;
            public int MediaType;
            public int DitherType;
            public int Reserved1;
            public int Reserved2;
            public int PanningWidth;
            public int PanningHeight;

            public static DeviceMode Create()
            {
                return new DeviceMode
                {
                    Size = (short)Marshal.SizeOf<DeviceMode>(),
                    DeviceName = string.Empty,
                    FormName = string.Empty
                };
            }
        }
    }
}
