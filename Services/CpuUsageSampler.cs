using System.Runtime.InteropServices;

namespace Heimdall.Services;

/// <summary>
/// Uso total de CPU via GetSystemTimes — diferença entre duas amostras, sem contador de
/// desempenho (PerformanceCounter demora ~1s pra inicializar e puxa WMI à toa).
/// </summary>
internal sealed class CpuUsageSampler
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    private long _idle, _kernel, _user;
    private bool _hasPrevious;

    /// <summary>0–100 desde a amostra anterior; null na primeira chamada ou se a API falhar.</summary>
    public double? Sample()
    {
        if (!GetSystemTimes(out long idle, out long kernel, out long user)) return null;

        double? result = null;
        if (_hasPrevious)
        {
            // Kernel inclui o tempo ocioso.
            long total = (kernel - _kernel) + (user - _user);
            long busy = total - (idle - _idle);
            if (total > 0) result = Math.Clamp(busy * 100.0 / total, 0, 100);
        }

        _idle = idle;
        _kernel = kernel;
        _user = user;
        _hasPrevious = true;
        return result;
    }
}
