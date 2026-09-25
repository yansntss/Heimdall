using System.Windows.Interop;

namespace Heimdall.Native;

/// <summary>
/// Hotkeys globais numa janela "message-only" própria, desacoplada do ciclo de vida
/// das BarWindow (sobrevive a Reload/troca de monitor). Ctrl+Shift+B (esconder/mostrar
/// a barra) é registrado aqui direto; outros hotkeys (ex.: mídia) entram via
/// <see cref="Register"/>.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int ShowHideId = 0xB001;
    private const uint VkB = 0x42;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = ShowHideId + 1;

    /// <summary>Ctrl+Shift+B — esconder/mostrar a barra manualmente, mesmo com outro app (jogo) em foco.</summary>
    public event Action? Pressed;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("Heimdall.HotkeyWindow")
        {
            WindowStyle = 0,
            ParentWindow = HwndMessage
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        NativeMethods.RegisterHotKey(_source.Handle, ShowHideId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, VkB);
    }

    /// <summary>Registra um hotkey global adicional. Retorna false se a combinação já está em uso por outro app.</summary>
    public bool Register(uint modifiers, uint vk, Action handler)
    {
        int id = _nextId++;
        bool ok = NativeMethods.RegisterHotKey(_source.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
        if (ok) _handlers[id] = handler;
        return ok;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY) return IntPtr.Zero;

        int id = wParam.ToInt32();
        if (id == ShowHideId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        else if (_handlers.TryGetValue(id, out var handler))
        {
            handler();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_source.Handle, ShowHideId);
        foreach (var id in _handlers.Keys) NativeMethods.UnregisterHotKey(_source.Handle, id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
