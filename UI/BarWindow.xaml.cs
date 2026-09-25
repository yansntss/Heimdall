using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;
using Heimdall.Widgets;

namespace Heimdall.UI;

public partial class BarWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly MonitorInfo _monitor;
    private readonly List<IWidget> _widgets = new();

    private bool IsVertical => _cfg.Edge is BarEdge.Left or BarEdge.Right;

    /// <summary>Null até a janela ter um HWND (depois de <see cref="OnSourceInitialized"/>).</summary>
    internal AppBarManager? AppBar { get; private set; }

    /// <summary>true = um app em tela cheia abriu nesse monitor; false = fechou.</summary>
    internal event Action<bool>? FullscreenChanged;

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
        BuildThemeMenu();

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Fora do Alt+Tab e da taskbar; nunca rouba ativação/foco
        NativeMethods.SetExStyle(hwnd,
            add: NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
            remove: NativeMethods.WS_EX_APPWINDOW);

        DwmVisuals.Apply(hwnd, ThemeService.GetEffectiveStyle(_cfg));

        AppBar = new AppBarManager(hwnd, _monitor, _cfg.Edge, Math.Clamp(_cfg.Thickness, 16, 400));
        AppBar.FullscreenChanged += fullscreen => FullscreenChanged?.Invoke(fullscreen);

        HwndSource.FromHwnd(hwnd)?.AddHook(AppBar.WndProc);
        AppBar.Register();

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

    private void OnClosed(object? sender, EventArgs e)
    {
        AppBar?.Dispose();
        foreach (var widget in _widgets) widget.Dispose();
        _widgets.Clear();
    }

    // ---------- Visual ----------

    private void ApplyStyle()
    {
        var style = ThemeService.GetEffectiveStyle(_cfg);

        var background = new SolidColorBrush(style.Background);
        background.Freeze();
        Root.Background = background;

        var foreground = new SolidColorBrush(style.Foreground);
        foreground.Freeze();
        TextElement.SetForeground(Root, foreground);

        TextElement.SetFontFamily(Root, new FontFamily(style.FontFamily));
        TextElement.SetFontSize(Root, style.FontSize);
    }

    // ---------- Widgets ----------

    private void BuildWidgets() =>
        _widgets.AddRange(WidgetZoneBuilder.Build(_cfg, IsVertical, Zones, StartZone, CenterZone, EndZone));

    // ---------- Menu de contexto ----------

    private static App CurrentApp => (App)Application.Current;

    private void BuildThemeMenu()
    {
        foreach (var name in ThemeService.GetAllThemeNames())
        {
            var item = new MenuItem
            {
                Header = name,
                IsCheckable = true,
                IsChecked = string.Equals(name, _cfg.Theme, StringComparison.OrdinalIgnoreCase),
                Tag = name
            };
            item.Click += (sender, _) => CurrentApp.SetTheme((string)((MenuItem)sender).Tag);
            ThemeMenu.Items.Add(item);
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenSettings();

    private void OpenConfig_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenConfig();

    private void OpenReminderHistory_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenReminderHistory();

    private void Reload_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(CurrentApp.Reload));

    private void Monitors_Click(object sender, RoutedEventArgs e) => CurrentApp.ShowMonitors();

    private void Exit_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(CurrentApp.ExitApp));
}
