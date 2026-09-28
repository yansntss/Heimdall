using System.Text;
using Heimdall.Native;

namespace Heimdall.Services;

/// <summary>
/// Enumeração/controle das janelas de topo "reais" (visíveis, sem dono, fora de
/// toolwindow) — base compartilhada pelo indicador de app aberto do launcher e pelo
/// widget de janelas abertas (windows). Fora de escopo de propósito:
/// DwmRegisterThumbnail (peek de miniatura), jump list.
/// </summary>
internal static class WindowTrackerService
{
    public sealed record TrackedWindow(IntPtr Handle, string ExecutablePath, string Title);

    public static bool IsTrackableTopLevelWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd)) return false;
        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero) return false;
        if ((NativeMethods.GetExStyle(hWnd) & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;
        return true;
    }

    /// <summary>Todas as janelas de topo "reais" no momento, na ordem de Z (frente pra trás) do EnumWindows.</summary>
    public static List<TrackedWindow> SnapshotAll(bool excludeCurrentProcess = true)
    {
        var result = new List<TrackedWindow>();
        uint currentPid = (uint)Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsTrackableTopLevelWindow(hWnd)) return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (excludeCurrentProcess && pid == currentPid) return true;

            if (TryGetExecutablePath(hWnd) is { } path)
                result.Add(new TrackedWindow(hWnd, path, GetTitle(hWnd)));

            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>Caminho do executável (normalizado) → HWND da primeira janela de topo encontrada — não trata múltiplas janelas do mesmo app.</summary>
    public static Dictionary<string, IntPtr> SnapshotFirstPerExecutable(bool excludeCurrentProcess = true)
    {
        var result = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in SnapshotAll(excludeCurrentProcess))
            if (!result.ContainsKey(window.ExecutablePath))
                result[window.ExecutablePath] = window.Handle;
        return result;
    }

    public static string GetTitle(IntPtr hWnd)
    {
        int length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0) return "";

        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public static bool IsForeground(IntPtr hWnd) => NativeMethods.GetForegroundWindow() == hWnd;

    /// <summary>Restaura se minimizada e tenta focar — se o app for elevado e o Heimdall não, SetForegroundWindow falha silenciosamente (limitação do Windows, sem contorno sem elevar o Heimdall também).</summary>
    public static void FocusOrRestore(IntPtr hWnd)
    {
        if (NativeMethods.IsIconic(hWnd)) NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hWnd);
    }

    public static void Minimize(IntPtr hWnd) => NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);

    /// <summary>Pede pra janela fechar (WM_CLOSE assíncrono) — não força; um app com alteração não salva pode ignorar ou perguntar antes.</summary>
    public static void Close(IntPtr hWnd) => NativeMethods.PostMessage(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    private static string? TryGetExecutablePath(IntPtr hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return null;

        IntPtr process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;

        try
        {
            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }
}
