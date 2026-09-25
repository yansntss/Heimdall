using System.Windows.Interop;

namespace InfoBar.Native;

/// <summary>
/// Hotkey global (Ctrl+Shift+B) para esconder/mostrar a barra manualmente,
/// mesmo com outro app (jogo) em foco. Usa uma janela "message-only" própria,
/// desacoplada do ciclo de vida das BarWindow (sobrevive a Reload/troca de monitor).
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0xB001;
    private const uint VkB = 0x42;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;

    public event Action? Pressed;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("InfoBar.HotkeyWindow")
        {
            WindowStyle = 0,
            ParentWindow = HwndMessage
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        NativeMethods.RegisterHotKey(_source.Handle, HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, VkB);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
