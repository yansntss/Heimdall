using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;
using Heimdall.UI;
using Microsoft.Win32;
using Windows.Media.Control;

namespace Heimdall;

public partial class App : Application
{
    private readonly List<BarPresenter> _bars = new();
    private Mutex? _mutex;
    private DispatcherTimer? _displayDebounce;
    private HotkeyManager? _hotkeys;
    private SettingsWindow? _settingsWindow;
    private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "Heimdall.SingleInstance", out bool created);
        if (!created)
        {
            Shutdown();
            return;
        }

        // Mudança de resolução / monitor conectado: reconstrói as barras (com debounce)
        _displayDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _displayDebounce.Tick += (_, _) =>
        {
            _displayDebounce.Stop();
            BuildBars();
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _hotkeys = new HotkeyManager();
        _hotkeys.Pressed += ToggleBarsVisibility;
        RegisterMediaHotkeys();
        _ = InitMediaManagerAsync();

        BuildBars();
    }

    // ---------- Atalhos globais de mídia (opcionais, Ctrl+Alt+...) ----------
    // Agem na sessão SMTC atual direto (não num widget específico), então funcionam
    // mesmo sem o widget "media" na barra ou com várias barras em monitores diferentes.

    private void RegisterMediaHotkeys()
    {
        const uint mod = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
        _hotkeys!.Register(mod, NativeMethods.VK_SPACE, MediaPlayPause);
        _hotkeys.Register(mod, NativeMethods.VK_LEFT, MediaPrevious);
        _hotkeys.Register(mod, NativeMethods.VK_RIGHT, MediaNext);
        _hotkeys.Register(mod, NativeMethods.VK_UP, () => AdjustMediaVolume(+0.05f));
        _hotkeys.Register(mod, NativeMethods.VK_DOWN, () => AdjustMediaVolume(-0.05f));
    }

    private async Task InitMediaManagerAsync()
    {
        try { _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
        catch { /* SMTC indisponível: atalhos de mídia viram no-op */ }
    }

    private void MediaPlayPause() => _ = _mediaManager?.GetCurrentSession()?.TryTogglePlayPauseAsync();

    private void MediaPrevious() => _ = _mediaManager?.GetCurrentSession()?.TrySkipPreviousAsync();

    private void MediaNext() => _ = _mediaManager?.GetCurrentSession()?.TrySkipNextAsync();

    private void AdjustMediaVolume(float delta)
    {
        string? app = _mediaManager?.GetCurrentSession()?.SourceAppUserModelId;
        float current = AudioVolumeService.GetVolume(app);
        AudioVolumeService.SetVolume(app, Math.Clamp(current + delta, 0f, 1f));
    }

    private void ToggleBarsVisibility()
    {
        bool target = !_bars.Any(b => b.IsVisible);
        foreach (var bar in _bars) bar.SetHidden(!target);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => RestartDebounce();

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Categorias que realmente indicam troca de tema/cor do SO. "Desktop" fica de
        // fora de propósito: registrar/desregistrar o AppBar muda a work area, e o
        // Windows dispara UserPreferenceChanged(Desktop) pra isso — sem esse filtro,
        // com um tema "vivo" (Auto/Destaque do Windows) isso virava um loop infinito de
        // BuildBars() → AppBar registra → Desktop muda → BuildBars() de novo.
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
            return;

        // Só recarrega se o tema atual acompanha o SO ("Auto" ou "Destaque do Windows") —
        // outras mudanças de preferência do usuário não afetam a barra.
        if (ThemeService.IsLiveTheme(ConfigService.Load().Theme)) RestartDebounce();
    }

    private void RestartDebounce() =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _displayDebounce!.Stop();
            _displayDebounce.Start();
        }));

    private void BuildBars()
    {
        CloseBars();

        var cfg = ConfigService.Load();
        var monitors = MonitorService.GetMonitors();
        if (monitors.Count == 0) return;

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];

        IEnumerable<MonitorInfo> targets = cfg.MonitorMode switch
        {
            MonitorMode.All => monitors,
            MonitorMode.Specific => new[]
            {
                monitors.FirstOrDefault(m => string.Equals(
                    m.ShortName,
                    MonitorInfo.Normalize(cfg.MonitorDevice),
                    StringComparison.OrdinalIgnoreCase)) ?? primary
            },
            _ => new[] { primary }
        };

        foreach (var monitor in targets)
        {
            var presenter = new BarPresenter(cfg, monitor);
            _bars.Add(presenter);
            presenter.Show();
        }
    }

    private void CloseBars()
    {
        foreach (var bar in _bars) bar.Close();
        _bars.Clear();
    }

    // ---- Ações chamadas pelo menu de contexto da barra ----

    public void Reload() => BuildBars();

    public void SetTheme(string name)
    {
        var cfg = ConfigService.Load();
        cfg.Theme = name;

        // Trocar pelo menu rápido é um "usa este tema inteiro" — limpa overrides
        // antigos de cor/fonte que, senão, ficariam escondendo a troca de tema.
        // Overrides finos continuam disponíveis na tela de Configurações.
        cfg.Style.Background = null;
        cfg.Style.Foreground = null;
        cfg.Style.FontFamily = null;
        cfg.Style.FontSize = null;

        ConfigService.Save(cfg);
        Reload();
    }

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    public void OpenConfig()
    {
        ConfigService.EnsureExists();
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{ConfigService.ConfigPath}\"") { UseShellExecute = true });
    }

    public void OpenReminderHistory()
    {
        Directory.CreateDirectory(ReminderHistoryService.HistoryDir);
        Process.Start(new ProcessStartInfo(ReminderHistoryService.HistoryDir) { UseShellExecute = true });
    }

    public void ShowMonitors()
    {
        var lines = MonitorService.GetMonitors().Select(m =>
            $"{m.ShortName}  —  {m.Bounds.Width}x{m.Bounds.Height} em ({m.Bounds.Left}, {m.Bounds.Top})" +
            (m.IsPrimary ? "  [principal]" : ""));

        MessageBox.Show(
            "Use o nome em \"MonitorDevice\" com \"MonitorMode\": \"Specific\":\n\n" +
            string.Join("\n", lines) +
            "\n\n(Ctrl+C copia este texto)",
            "Heimdall — Monitores");
    }

    public void ExitApp()
    {
        CloseBars();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _hotkeys?.Dispose();
        CloseBars();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
