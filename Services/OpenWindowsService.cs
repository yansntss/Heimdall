using System.Text;
using Heimdall.Native;

namespace Heimdall.Services;

/// <summary>
/// Snapshot das janelas de topo "reais" (visíveis, sem dono, fora de toolwindow) — usado
/// pelo launcher pra saber se um atalho já está aberto e focar a janela dele. Fora de
/// escopo de propósito: SetWinEventHook (fica só polling), DwmRegisterThumbnail (peek de
/// miniatura), jump list, fechar janela pelo menu, ícones temporários pra apps não fixados.
/// </summary>
internal static class OpenWindowsService
{
    /// <summary>Caminho do executável (normalizado) → HWND da primeira janela de topo encontrada — não trata múltiplas janelas do mesmo app.</summary>
    public static Dictionary<string, IntPtr> Snapshot()
    {
        var result = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero) return true;
            if ((NativeMethods.GetExStyle(hWnd) & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;

            if (TryGetExecutablePath(hWnd) is { } path && !result.ContainsKey(path))
                result[path] = hWnd;

            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>Restaura se minimizada e tenta focar — se o app for elevado e o Heimdall não, SetForegroundWindow falha silenciosamente (limitação do Windows, sem contorno sem elevar o Heimdall também).</summary>
    public static void FocusOrRestore(IntPtr hWnd)
    {
        if (NativeMethods.IsIconic(hWnd)) NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hWnd);
    }

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
