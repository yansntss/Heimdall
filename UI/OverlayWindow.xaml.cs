using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using InfoBar.Config;
using InfoBar.Native;
using InfoBar.Services;
using InfoBar.Widgets;

namespace InfoBar.UI;

/// <summary>
/// Janela de overlay em tela cheia: sempre transparente e click-through — o clique
/// atravessa direto pro jogo. Só existe pra mostrar texto por cima; nunca é a AppBar.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly List<IWidget> _widgets = new();

    private bool IsVertical => _cfg.Edge is BarEdge.Left or BarEdge.Right;

    internal OverlayWindow(AppConfig cfg, MonitorInfo monitor)
    {
        InitializeComponent();
        _cfg = cfg;

        // Fora da tela até a primeira chamada de ShowOnMonitor (evita "piscar")
        Left = -32000;
        Top = -32000;
        Width = 1;
        Height = 1;

        ApplyStyle();
        _widgets.AddRange(WidgetZoneBuilder.Build(_cfg, IsVertical, Zones, StartZone, CenterZone, EndZone));

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Sempre fora do Alt+Tab/taskbar, nunca ativa e sempre click-through — essa
        // janela só existe pro modo overlay, não precisa alternar estilo em runtime.
        NativeMethods.SetExStyle(hwnd,
            add: NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE
                 | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);
    }

    /// <summary>Mostra a janela cobrindo o monitor inteiro, sempre no topo.</summary>
    internal void ShowOnMonitor(MonitorInfo monitor)
    {
        if (Visibility != Visibility.Visible) Show();

        var hwnd = new WindowInteropHelper(this).Handle;
        var b = monitor.Bounds;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            b.Left, b.Top, b.Width, b.Height, NativeMethods.SWP_NOACTIVATE);
    }

    internal void HideAway() => Hide();

    private void OnClosed(object? sender, EventArgs e)
    {
        foreach (var widget in _widgets) widget.Dispose();
        _widgets.Clear();
    }

    private void ApplyStyle()
    {
        var style = ThemeService.GetEffectiveStyle(_cfg);

        var foreground = new SolidColorBrush(style.Foreground);
        foreground.Freeze();
        TextElement.SetForeground(Root, foreground);
        TextElement.SetFontFamily(Root, new FontFamily(style.FontFamily));
        TextElement.SetFontSize(Root, style.FontSize);
    }
}
