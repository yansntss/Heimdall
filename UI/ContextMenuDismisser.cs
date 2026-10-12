using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Native;

namespace Heimdall.UI;

/// <summary>
/// Fecha menus de contexto ao clicar fora do Heimdall. A barra é WS_EX_NOACTIVATE: o app
/// nunca fica "ativo", então o WPF não percebe o clique em outra janela (perda de ativação)
/// e o menu ficava aberto até escolher um item ou clicar de novo na barra.
/// </summary>
internal static class ContextMenuDismisser
{
    private const int PollMs = 30;
    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const int VK_MBUTTON = 0x04;

    private static readonly HashSet<ContextMenu> OpenMenus = new();
    private static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(PollMs) };
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;
    private static bool _wasPressed;

    public static void Install()
    {
        Timer.Tick += (_, _) => Poll();
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnOpened));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent, new RoutedEventHandler(OnClosed));
    }

    private static void OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        OpenMenus.Add(menu);
        // O clique direito que abriu o menu pode ainda estar pressionado — só conta um
        // clique novo, depois de soltar.
        _wasPressed = AnyButtonPressed();
        Timer.Start();
    }

    private static void OnClosed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu) OpenMenus.Remove(menu);
        if (OpenMenus.Count == 0) Timer.Stop();
    }

    private static void Poll()
    {
        bool pressed = AnyButtonPressed();
        bool newPress = pressed && !_wasPressed;
        _wasPressed = pressed;
        if (!newPress || !NativeMethods.GetCursorPos(out var cursor)) return;

        // Clique numa janela nossa (o próprio menu, submenu, a barra) o WPF já trata.
        NativeMethods.GetWindowThreadProcessId(NativeMethods.WindowFromPoint(cursor), out uint pid);
        if (pid == OwnProcessId) return;

        foreach (var menu in OpenMenus.ToList()) menu.IsOpen = false;
    }

    private static bool AnyButtonPressed() =>
        (NativeMethods.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;
}
