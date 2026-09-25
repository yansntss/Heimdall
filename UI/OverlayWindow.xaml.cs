using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;
using Heimdall.Widgets;

namespace Heimdall.UI;

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
        ApplyLayout();
        _widgets.AddRange(WidgetZoneBuilder.Build(_cfg, IsVertical, Zones, StartZone, CenterZone, EndZone, isOverlay: true));

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

    /// <summary>
    /// A janela de overlay cobre o monitor inteiro (precisa, pro click-through funcionar
    /// em qualquer parte da tela) — sem isso o Root ficava esticado pra célula toda e as
    /// zonas (Start/Center/End) centralizavam no meio da tela em vez de ficar na borda.
    /// Ancora o Root na borda configurada com a mesma espessura da AppBar normal.
    /// </summary>
    private void ApplyLayout()
    {
        double thickness = Math.Clamp(_cfg.Thickness, 16, 400);

        switch (_cfg.Edge)
        {
            case BarEdge.Top:
                Root.HorizontalAlignment = HorizontalAlignment.Stretch;
                Root.VerticalAlignment = VerticalAlignment.Top;
                Root.Height = thickness;
                Root.Width = double.NaN;
                break;
            case BarEdge.Bottom:
                Root.HorizontalAlignment = HorizontalAlignment.Stretch;
                Root.VerticalAlignment = VerticalAlignment.Bottom;
                Root.Height = thickness;
                Root.Width = double.NaN;
                break;
            case BarEdge.Left:
                Root.HorizontalAlignment = HorizontalAlignment.Left;
                Root.VerticalAlignment = VerticalAlignment.Stretch;
                Root.Width = thickness;
                Root.Height = double.NaN;
                break;
            case BarEdge.Right:
                Root.HorizontalAlignment = HorizontalAlignment.Right;
                Root.VerticalAlignment = VerticalAlignment.Stretch;
                Root.Width = thickness;
                Root.Height = double.NaN;
                break;
        }
    }
}
