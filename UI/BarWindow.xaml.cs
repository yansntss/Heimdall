using System.IO;
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
using Microsoft.Win32;

namespace Heimdall.UI;

public partial class BarWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly MonitorInfo _monitor;
    private readonly List<IWidget> _widgets = new();
    private InstalledAppPickerWindow? _appPicker;
    private Point _lastRightClickScreenPoint;

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
        BuildAddWidgetMenu();
        UpdateGamingModeMenuItem();

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

        DwmVisuals.Apply(hwnd, ThemeService.GetEffectiveStyle(_cfg), forceRoundedCorners: _cfg.FloatingMode);

        int floatingMargin = _cfg.FloatingMode ? Math.Clamp(_cfg.FloatingMargin, 0, 100) : 0;
        AppBar = new AppBarManager(hwnd, _monitor, _cfg.Edge, Math.Clamp(_cfg.Thickness, 16, 400), floatingMargin);
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

    /// <summary>Adicionar widget direto pelo menu da barra, sem precisar abrir Configurações — mesmo catálogo da aba Widgets.</summary>
    private void BuildAddWidgetMenu()
    {
        foreach (var (id, icon) in WidgetFactory.Catalog)
        {
            var item = new MenuItem { Header = $"{icon}  {id}", Tag = id };
            item.Click += (sender, _) => CurrentApp.AddWidget((string)((MenuItem)sender).Tag);
            AddWidgetMenu.Items.Add(item);
        }
    }

    private void UpdateGamingModeMenuItem()
    {
        GamingModeMenuItem.Header = _cfg.GamingMode ? "Modo gaming: Ativado" : "Modo gaming: Desativado";
        GamingModeMenuItem.IsChecked = _cfg.GamingMode;
    }

    private void GamingMode_Click(object sender, RoutedEventArgs e) => CurrentApp.ToggleGamingMode();

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenSettings();

    private void OpenConfig_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenConfig();

    private void OpenReminderHistory_Click(object sender, RoutedEventArgs e) => CurrentApp.OpenReminderHistory();

    private void Reload_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(CurrentApp.Reload));

    private void Monitors_Click(object sender, RoutedEventArgs e) => CurrentApp.ShowMonitors();

    private void Exit_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(CurrentApp.ExitApp));

    // ---------- Adicionar atalho ----------

    private void AddLauncherFromFile_Click(object sender, RoutedEventArgs e)
    {
        // Nem sem owner nem com esta janela (WS_EX_NOACTIVATE) como dono o diálogo aparece
        // de verdade: o processo nunca tem uma janela "ativa" de verdade pro Explorer usar
        // como referência de foreground. Um Window normal (ativável) temporário como dono
        // resolve — cria, ativa, mostra o diálogo, fecha o auxiliar.
        var helper = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Width = 0,
            Height = 0,
            Opacity = 0
        };
        helper.Show();
        helper.Activate();

        var dialog = new OpenFileDialog { Title = "Escolher arquivo ou atalho", Filter = "Todos os arquivos|*.*" };
        bool chosen = dialog.ShowDialog(helper) == true;
        helper.Close();
        if (!chosen) return;

        CurrentApp.AddLauncher(new LauncherConfig
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            Path = dialog.FileName
        });
    }

    private void AddLauncherFromInstalled_Click(object sender, RoutedEventArgs e)
    {
        if (_appPicker is not null)
        {
            _appPicker.Activate();
            return;
        }

        _appPicker = new InstalledAppPickerWindow(ThemeService.GetEffectiveStyle(_cfg));
        _appPicker.Chosen += app => CurrentApp.AddLauncher(new LauncherConfig
        {
            Name = app.Name,
            Path = $"shell:AppsFolder\\{app.AppUserModelId}"
        });
        _appPicker.Closed += (_, _) => _appPicker = null;
        _appPicker.Show();
    }

    private void Root_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _lastRightClickScreenPoint = PointToScreen(e.GetPosition(this));

    private void AddSeparator_Click(object sender, RoutedEventArgs e)
    {
        var launcherWidget = _widgets.OfType<LauncherWidget>().FirstOrDefault();

        // Sem o widget "launcher" ainda na barra: cai no fim, e o AddLauncherAt/EnsureLauncherWidgetVisible
        // cuida de fazer ele aparecer em algum lugar (igual "Adicionar atalho" já faz).
        int index = 0;
        if (launcherWidget is not null)
        {
            var pointInWidget = launcherWidget.View.PointFromScreen(_lastRightClickScreenPoint);
            index = launcherWidget.GetInsertIndex(pointInWidget);
        }

        CurrentApp.AddLauncherAt(new LauncherConfig { Type = LauncherItemType.Separator, Name = "Separador" }, index);
    }

    private void Root_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Root_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        CurrentApp.AddLaunchers(paths.Select(path => new LauncherConfig
        {
            Name = Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/')),
            Path = path
        }));
    }
}
