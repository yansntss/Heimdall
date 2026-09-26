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
    private ReminderHistoryWindow? _historyWindow;

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
        _hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, NativeMethods.VK_R,
            () => Widgets.ReminderWidget.Primary?.OpenQuickAdd());

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

    // GlobalSystemMediaTransportControlsSessionManager.RequestAsync() puxa ~45MB de DLLs de
    // projeção WinRT (Microsoft.Windows.SDK.NET.dll sozinha já são 24MB, mais
    // windows.storage.dll, OneCoreUAPCommonProxyStub.dll etc.) — custo real medido, não só
    // teórico. Antes isso rodava sempre no OnStartup, mesmo pra quem não usa nem o widget
    // "media" nem os atalhos. Agora só inicializa na primeira vez que um atalho de mídia é
    // de fato apertado — quem nunca usa mídia nunca paga esse custo. O widget "media" (se
    // configurado) continua com sua própria instância independente, inicializada só quando
    // o widget existe de verdade.
    private Task<GlobalSystemMediaTransportControlsSessionManager?>? _mediaManagerTask;

    private Task<GlobalSystemMediaTransportControlsSessionManager?> GetMediaManagerAsync() =>
        _mediaManagerTask ??= RequestMediaManagerAsync();

    private static async Task<GlobalSystemMediaTransportControlsSessionManager?> RequestMediaManagerAsync()
    {
        try { return await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
        catch { return null; } // SMTC indisponível: atalhos de mídia viram no-op
    }

    private async void MediaPlayPause() => _ = (await GetMediaManagerAsync())?.GetCurrentSession()?.TryTogglePlayPauseAsync();

    private async void MediaPrevious() => _ = (await GetMediaManagerAsync())?.GetCurrentSession()?.TrySkipPreviousAsync();

    private async void MediaNext() => _ = (await GetMediaManagerAsync())?.GetCurrentSession()?.TrySkipNextAsync();

    private async void AdjustMediaVolume(float delta)
    {
        var manager = await GetMediaManagerAsync();
        string? app = manager?.GetCurrentSession()?.SourceAppUserModelId;
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

    /// <summary>Atalho rápido do menu de clique direito — inverte GamingMode e salva na hora, sem precisar abrir Configurações.</summary>
    public void ToggleGamingMode()
    {
        var cfg = ConfigService.Load();
        cfg.GamingMode = !cfg.GamingMode;
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

    /// <summary>Adiciona um atalho ao config e recarrega — garante que o widget "launcher" apareça em algum lugar da barra.</summary>
    public void AddLauncher(LauncherConfig launcher) => AddLaunchers(new[] { launcher });

    /// <summary>Mesma coisa, em lote — evita recarregar a barra uma vez por item ao soltar vários arquivos de uma vez.</summary>
    public void AddLaunchers(IEnumerable<LauncherConfig> launchers)
    {
        var cfg = ConfigService.Load();
        cfg.Launchers.AddRange(launchers);
        EnsureLauncherWidgetVisible(cfg);
        ConfigService.Save(cfg);
        Reload();
    }

    /// <summary>Insere um item (atalho ou separador) numa posição específica — usado pelo "Adicionar separador" pra cair onde o usuário clicou.</summary>
    public void AddLauncherAt(LauncherConfig item, int index)
    {
        var cfg = ConfigService.Load();
        cfg.Launchers.Insert(Math.Clamp(index, 0, cfg.Launchers.Count), item);
        EnsureLauncherWidgetVisible(cfg);
        ConfigService.Save(cfg);
        Reload();
    }

    private static void EnsureLauncherWidgetVisible(AppConfig cfg)
    {
        bool alreadyVisible = cfg.Widgets.Start.Any(w => w.Id == "launcher")
            || cfg.Widgets.Center.Any(w => w.Id == "launcher")
            || cfg.Widgets.End.Any(w => w.Id == "launcher");
        if (!alreadyVisible) cfg.Widgets.End.Add(new WidgetEntry("launcher"));
    }

    public void OpenReminderHistory()
    {
        if (_historyWindow is not null)
        {
            _historyWindow.Activate();
            return;
        }

        _historyWindow = new ReminderHistoryWindow();
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
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
