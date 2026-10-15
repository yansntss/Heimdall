using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;

namespace Heimdall.UI.Popups;

/// <summary>
/// Dropdown da barra (mídia, lembretes, sistema): card de 300px ancorado no item que o
/// abriu, do lado de dentro da tela (abaixo numa barra no topo, à direita numa barra à
/// esquerda etc.). É uma janela ativável — não um Popup — pra receber teclado (Tab, Esc)
/// mesmo aberta a partir da barra, que nunca fica ativa (WS_EX_NOACTIVATE). Fecha com Esc
/// ou ao perder o foco; clicar de novo no mesmo item fecha (toggle).
/// </summary>
internal sealed class FlyoutWindow : Window
{
    /// <summary>Espaço transparente em volta do card pra sombra (0 18 40) não ser cortada.</summary>
    private static readonly Thickness ShadowMargin = new(30, 14, 30, 50);
    private const double Gap = 6;
    private const double ScreenPadding = 8;

    /// <summary>Clique no item que já fechou o dropdown (por perda de foco) não deve reabrir.</summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private static FlyoutWindow? _current;
    private static FrameworkElement? _lastClosedAnchor;
    private static DateTime _lastClosedAt;

    private readonly FrameworkElement _anchor;
    private readonly BarEdge _edge;
    private readonly Border _card;
    private bool _closing;
    private bool _everActivated;

    /// <summary>Abre o dropdown do item — ou fecha, se o aberto agora é dele.</summary>
    public static void Toggle(FrameworkElement anchor, BarEdge edge, Func<FrameworkElement> createContent, string title)
    {
        if (_current is { } open)
        {
            bool same = ReferenceEquals(open._anchor, anchor);
            open.Close();
            if (same) return;
        }

        if (ReferenceEquals(_lastClosedAnchor, anchor) && DateTime.UtcNow - _lastClosedAt < ReopenGuard) return;

        var window = new FlyoutWindow(anchor, edge, createContent(), title);
        _current = window;
        window.Show();
        window.Activate();
    }

    public static void CloseCurrent() => _current?.Close();

    private FlyoutWindow(FrameworkElement anchor, BarEdge edge, FrameworkElement content, string title)
    {
        _anchor = anchor;
        _edge = edge;

        Title = title;
        Icon = AppIcon.Source;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Opacity = 0;

        // Fora da tela até a posição final ser calculada (evita "piscar" no canto errado)
        Left = -32000;
        Top = -32000;

        _card = new Border { Child = content };
        _card.SetResourceReference(StyleProperty, "H.PopupCard");

        var root = new Border { Padding = ShadowMargin, Child = _card };
        root.SetResourceReference(TextElement.FontFamilyProperty, "H.Font.UI");
        root.SetResourceReference(TextElement.ForegroundProperty, "H.Brush.Text");
        TextOptions.SetTextFormattingMode(root, TextFormattingMode.Display);
        KeyboardNavigation.SetTabNavigation(root, KeyboardNavigationMode.Cycle);
        Content = root;

        Loaded += (_, _) => { Reposition(); FadeIn(); };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(Reposition));

        // Mesmas guardas do CalendarWindow: Close() reentrante via Deactivated derruba o
        // app, e aberta a partir da barra (NOACTIVATE) o Windows às vezes manda um
        // Deactivated espúrio antes da janela chegar a ser ativada.
        Activated += (_, _) =>
        {
            _everActivated = true;
            // Primeiro Tab já cai no primeiro controle do card.
            if (Keyboard.FocusedElement is null) MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        };
        Deactivated += (_, _) => { if (!_closing && _everActivated) Close(); };
        Closing += (_, _) => _closing = true;
        Closed += OnClosed;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        };
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_current, this)) _current = null;
        _lastClosedAnchor = _anchor;
        _lastClosedAt = DateTime.UtcNow;
        if (_card.Child is FrameworkElement { DataContext: IDisposable vm }) vm.Dispose();
    }

    private void FadeIn() =>
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));

    /// <summary>
    /// Tudo em pixels físicos (SetWindowPos), na escala do monitor da âncora: Left/Top em
    /// DIPs ficam ambíguos com monitores de DPI diferente (per-monitor v2).
    /// </summary>
    private void Reposition()
    {
        if (PresentationSource.FromVisual(_anchor) is not { CompositionTarget: { } target }) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        double scale = target.TransformToDevice.M11;
        var anchorTopLeft = _anchor.PointToScreen(new Point(0, 0));
        double anchorW = _anchor.ActualWidth * scale, anchorH = _anchor.ActualHeight * scale;

        // A extensão perpendicular vem da janela da barra inteira, não do widget (que é
        // menor que a espessura e fica centralizado nela).
        var bar = GetWindow(_anchor);
        var barTopLeft = bar?.PointToScreen(new Point(0, 0)) ?? anchorTopLeft;
        double barW = (bar?.ActualWidth ?? _anchor.ActualWidth) * scale;
        double barH = (bar?.ActualHeight ?? _anchor.ActualHeight) * scale;

        double cardW = _card.ActualWidth * scale, cardH = _card.ActualHeight * scale, gap = Gap * scale;
        double x, y;
        switch (_edge)
        {
            case BarEdge.Bottom:
                x = anchorTopLeft.X + anchorW / 2 - cardW / 2;
                y = barTopLeft.Y - gap - cardH;
                break;
            case BarEdge.Left:
                x = barTopLeft.X + barW + gap;
                y = anchorTopLeft.Y + anchorH / 2 - cardH / 2;
                break;
            case BarEdge.Right:
                x = barTopLeft.X - gap - cardW;
                y = anchorTopLeft.Y + anchorH / 2 - cardH / 2;
                break;
            default: // Top
                x = anchorTopLeft.X + anchorW / 2 - cardW / 2;
                y = barTopLeft.Y + barH + gap;
                break;
        }

        // Item perto da ponta da barra: o card não pode passar da borda do monitor.
        var handle = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT { X = (int)(anchorTopLeft.X + anchorW / 2), Y = (int)(anchorTopLeft.Y + anchorH / 2) },
            NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (MonitorService.GetMonitors().FirstOrDefault(m => m.Handle == handle) is { } monitor)
        {
            double pad = ScreenPadding * scale;
            var b = monitor.Bounds;
            x = Math.Max(b.Left + pad, Math.Min(x, b.Right - pad - cardW));
            y = Math.Max(b.Top + pad, Math.Min(y, b.Bottom - pad - cardH));
        }

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            (int)Math.Round(x - ShadowMargin.Left * scale), (int)Math.Round(y - ShadowMargin.Top * scale),
            0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
