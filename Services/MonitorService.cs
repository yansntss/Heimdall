using System.Runtime.InteropServices;
using InfoBar.Native;

namespace InfoBar.Services;

internal sealed record MonitorInfo(IntPtr Handle, string DeviceName, NativeMethods.RECT Bounds, bool IsPrimary)
{
    /// <summary>"\\.\DISPLAY1" → "DISPLAY1"</summary>
    public string ShortName => Normalize(DeviceName);

    public static string Normalize(string? name) =>
        (name ?? string.Empty).Replace(@"\\.\", string.Empty).Trim();
}

internal static class MonitorService
{
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                var info = new NativeMethods.MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                };

                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    list.Add(new MonitorInfo(
                        hMonitor,
                        info.szDevice,
                        info.rcMonitor,
                        (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
                }
                return true;
            }, IntPtr.Zero);

        return list;
    }
}
