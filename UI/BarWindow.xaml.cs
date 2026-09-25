using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InfoBar.Config;
using InfoBar.Native;
using InfoBar.Services;
using InfoBar.Widgets;

namespace InfoBar.UI;

public partial class BarWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly MonitorInfo _monitor;
    private readonly List<IWidget> _widgets = new();
    private AppBarManager? _appBar;
    private bool _overlay;

    private bool IsVertical => _cfg.Edge is BarEdge.Left or BarEdge.Right;

    internal BarWindow(AppConfig cfg, MonitorInfo monitor)
    {
        InitializeComponent();
        _cfg = cfg;
        _monitor = monitor;

        // Fora da tela até o AppBar definir a posição real (evita "piscar")
        Left = -32000;
        Top = -32000;
        Width = 1;
        Height = 1;
        Opacity = 0;

        ApplyStyle();
        BuildWidgets();

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Fora do Alt+Tab e da taskbar; nunca rouba ativação/foco (nem barra, nem overlay)
        NativeMethods.SetExStyle(hwnd,
            add: NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
            remove: NativeMethods.WS_EX_APPWINDOW);

        _appBar = new AppBarManager(hwnd, _monitor, _cfg.Edge, Math.Clamp(_cfg.Thickness, 16, 400));

        _appBar.FullscreenChanged += fullscreen =>
        {
            if (fullscreen) EnterOverlay();
            else ExitOverlay();
        };

        HwndSource.FromHwnd(hwnd)?.AddHook(_appBar.WndProc);
        _appBar.Register();

        FadeIn();
    }

    private void FadeIn()
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, animation);
    }

    // ---------- Modo overlay (tela cheia) ----------

    private void EnterOverlay()
    {
        if (_overlay) return;
        _overlay = true;

        _appBar?.Unregister();
        Root.Background = Brushes.Transparent;

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetExStyle(hwnd, add: NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);

        var b = _monitor.Bounds;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            b.Left, b.Top, b.Width, b.Height, NativeMethods.SWP_NOACTIVATE);
    }

    private void ExitOverlay()
    {
        if (!_overlay) return;
        _overlay = false;

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetExStyle(hwnd, remove: NativeMethods.WS_EX_TRANSPARENT);

        ApplyStyle();
        _appBar?.Register();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _appBar?.Dispose();
        foreach (var widget in _widgets) widget.Dispose();
        _widgets.Clear();
    }

    // ---------- Visual ----------

    private void ApplyStyle()
    {
        var style = _cfg.Style;
        Root.Background = ParseBrush(style.Background, Color.FromArgb(0xE6, 0x1E, 0x1E, 0x1E));
        TextElement.SetForeground(Root, ParseBrush(style.Foreground, Colors.White));
        TextElement.SetFontFamily(Root, new FontFamily(string.IsNullOrWhiteSpace(style.FontFamily) ? "Segoe UI" : style.FontFamily));
        TextElement.SetFontSize(Root, Math.Clamp(style.FontSize, 8, 72));
    }

    private static SolidColorBrush ParseBrush(string? value, Color fallback)
    {
        Color color = fallback;
        try
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorConverter.ConvertFromString(value) is Color parsed)
                color = parsed;
        }
        catch (FormatException) { }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // ---------- Widgets ----------

    private void BuildWidgets()
    {
        bool vertical = IsVertical;
        var orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;

        Zones.Margin = vertical ? new Thickness(0, 8, 0, 8) : new Thickness(8, 0, 8, 0);

        SetupZone(StartZone, _cfg.Widgets.Start, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            vertical ? VerticalAlignment.Top : VerticalAlignment.Center);

        SetupZone(CenterZone, _cfg.Widgets.Center, orientation,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        SetupZone(EndZone, _cfg.Widgets.End, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center);
    }

    private void SetupZone(StackPanel zone, IEnumerable<string>? ids, Orientation orientation,
        HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        zone.Orientation = orientation;
        zone.HorizontalAlignment = horizontal;
        zone.VerticalAlignment = vertical;

        if (ids is null) return;

        foreach (var id in ids)
        {
            var widget = WidgetFactory.Create(id, _cfg);
            if (widget is null) continue;

            widget.ApplyOrientation(orientation);
            widget.View.Margin = orientation == Orientation.Vertical
                ? new Thickness(0, 4, 0, 4)
                : new Thickness(8, 0, 8, 0);

            zone.Children.Add(widget.View);
            _widgets.Add(widget);
            widget.Start();
        }
    }

    // ---------- Menu de contexto ----------

    private static App CurrentApp => (App)Application.Current;

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenSettings();

    private void OpenConfig_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenConfig();

    private void Reload_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(CurrentApp.Reload));

    private void Monitors_Click(object sender, RoutedEventArgs e) => CurrentApp.ShowMonitors();

    private void Exit_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(CurrentApp.ExitApp));
}
