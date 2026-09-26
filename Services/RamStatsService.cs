using Heimdall.Native;

namespace Heimdall.Services;

/// <summary>Uso de memória física via GlobalMemoryStatusEx — mais leve que PerformanceCounter, sem precisar inicializar contadores de desempenho.</summary>
internal static class RamStatsService
{
    public readonly record struct RamStats(double UsedGb, double TotalGb, double Percent);

    public static RamStats? GetStats()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status)) return null;

        const double bytesPerGb = 1024.0 * 1024.0 * 1024.0;
        double totalGb = status.ullTotalPhys / bytesPerGb;
        double availGb = status.ullAvailPhys / bytesPerGb;
        return new RamStats(totalGb - availGb, totalGb, status.dwMemoryLoad);
    }
}
