using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CvModManager.Lib
{
    internal static partial class NativeMethods
    {
        private static readonly Guid LocalLowId = new("A520A1A4-1780-4FF6-BD18-167343C5AF16");

#pragma warning disable SYSLIB1054 // LibraryImportAttribute
        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags, IntPtr hToken, out IntPtr pszPath);
#pragma warning restore SYSLIB1054

        private static string WindowsGetLocalLowPath()
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                int hr = SHGetKnownFolderPath(LocalLowId, 0, IntPtr.Zero, out pszPath);
                if (hr >= 0)
                {
                    return Marshal.PtrToStringAuto(pszPath) ?? throw new IOException("LocalLow patrh is null");
                }

                throw Marshal.GetExceptionForHR(hr) ?? new Win32Exception($"Windows API error {hr:X8}") { HResult = hr };
            }
            finally
            {
                if (pszPath != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pszPath);
                }
            }
        }

        [DoesNotReturn]
        private static string LinuxGetLocalLowPath()
        {
            throw new PlatformNotSupportedException("TODO: Linux path");
        }

        [DoesNotReturn]
        private static string MacGetLocalLowPath()
        {
            throw new PlatformNotSupportedException("TODO: Mac path");
        }

        internal static string GetLocalLowPath()
        {
            if (OperatingSystem.IsWindows())
            {
                return WindowsGetLocalLowPath();
            }
            if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
            {
                return LinuxGetLocalLowPath();
            }
            if (OperatingSystem.IsMacOS())
            {
                return MacGetLocalLowPath();
            }
            var field = typeof(OperatingSystem)
                .GetField("OSPlatformName", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetRawConstantValue()?.ToString()
                ?? "<unknown>";

            throw new PlatformNotSupportedException($"TODO: OS Type {field}");
        }
    }
}
