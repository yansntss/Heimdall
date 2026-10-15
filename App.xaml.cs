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
    private RemindersWindow? _remindersWindow;
    private DispatcherTimer? _updateTimer;
    private UpdateNotificationWindow? _updateNotification;
    private TrayIcon? _tray;
    private Services.Reminders.ReminderScheduler? _reminderScheduler;
    /// <summary>Borda da barra montada por último — onde o aviso de lembrete não deve aparecer.</summary>
    private BarEdge _barEdge = BarEdge.Top;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool afterGpuReset = e.Args.Contains(GpuResetArg);

        _mutex = new Mutex(true, "Heimdall.SingleInstance", out bool created);
        if (!created && !(afterGpuReset && WaitForPreviousInstance(_mutex)))
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        UI.ContextMenuDismisser.Install();
        ThemeManager.Initialize();

        // Relançado depois de um reset do driver de vídeo: dá um respiro pro driver
        // terminar de voltar antes de criar janelas novas (senão cai de novo na hora).
        if (afterGpuReset) Thread.Sleep(GpuResetSettleMs);

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
            ReminderActions.OpenNew);
        // Ctrl+Alt+H: ocultar/mostrar a barra (o Ctrl+Shift+B antigo continua valendo).
        _hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_H, ToggleBarsVisibility);

        BuildBars();
        _tray = new TrayIcon();
        StartReminderScheduler();
        StartUpdateChecks();
    }

    // ---------- Aviso de versão nova ----------

    private static readonly TimeSpan UpdateFirstCheckDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

    /// <summary>
    /// Primeira checagem alguns segundos depois de abrir (a barra já na tela, sem disputar
    /// o início) e depois a cada poucas horas — quem deixa o PC ligado dias seguidos
    /// também fica sabendo. Desligável por "CheckForUpdates": false no config.
    /// </summary>
    private void StartUpdateChecks()
    {
        if (!ConfigService.Load().CheckForUpdates) return;

        UpdateService.UpdateFound += ShowUpdateNotification;
        _updateTimer = new DispatcherTimer { Interval = UpdateFirstCheckDelay };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = UpdateCheckInterval;
            await UpdateService.CheckAsync();
        };
        _updateTimer.Start();
    }

    /// <summary>Uma vez por versão nova encontrada, colada no botão de download da primeira barra visível.</summary>
    private void ShowUpdateNotification(UpdateInfo update)
    {
        var bar = _bars.FirstOrDefault(b => b.IsShowingBar);
        if (bar is null || _updateNotification is not null) return; // em tela cheia: fica só o botão na barra

        var cfg = ConfigService.Load();
        var window = new UpdateNotificationWindow(ThemeService.GetEffectiveStyle(cfg), update);
        window.AnchorTo(bar.UpdateAnchor, cfg.Edge);
        window.Closed += (_, _) => _updateNotification = null;
        _updateNotification = window;
        window.Show();
    }

    // ---------- Reset do driver de vídeo (TDR) ----------
    // Quando a GPU trava e o Windows reinicia o driver (tela preta por uns segundos), o
    // thread de renderização do WPF morre com UCEERR_RENDERTHREADFAILURE e não tem volta
    // dentro do mesmo processo — antes isso derrubava o Heimdall e a barra sumia até o
    // usuário abrir de novo. Agora o app se relança sozinho.

    private const string GpuResetArg = "--after-gpu-reset";
    private const int GpuResetSettleMs = 2000;
    private const int UCEERR_RENDERTHREADFAILURE = unchecked((int)0x88980406);

    /// <summary>Se a instância nova também cair logo de cara (driver ainda instável), não relança de novo — evita loop.</summary>
    private static readonly TimeSpan MinUptimeForRestart = TimeSpan.FromSeconds(15);

    private bool _restarting;

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!IsRenderThreadFailure(e.Exception)) return;

        var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
        if (uptime < MinUptimeForRestart || Environment.ProcessPath is not { } exe) return;

        e.Handled = true;
        if (_restarting) return;
        _restarting = true;

        try
        {
            Process.Start(new ProcessStartInfo(exe, GpuResetArg) { UseShellExecute = false });
        }
        catch
        {
            _restarting = false;
            e.Handled = false; // sem como relançar: deixa cair como antes
            return;
        }

        // Não passa pelo CloseBars(): mexer nas janelas agora só provoca a mesma exceção
        // de novo. O processo sai e o Windows limpa janelas/AppBar/hotkeys sozinho.
        Environment.Exit(0);
    }

    private static bool IsRenderThreadFailure(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex.HResult == UCEERR_RENDERTHREADFAILURE) return true;
        return false;
    }

    /// <summary>A instância anterior ainda pode estar saindo quando a nova sobe — espera ela soltar o mutex.</summary>
    private static bool WaitForPreviousInstance(Mutex mutex)
    {
        try { return mutex.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { return true; } // saiu sem liberar (Environment.Exit): o mutex é nosso
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

    public void ToggleBarsVisibility()
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

        // Só reaplica se o acento acompanha a cor de destaque do Windows ("system") — outras
        // mudanças de preferência não afetam a barra. Pincéis são DynamicResource: reaplicar o
        // tema já basta, sem recriar as barras.
        var cfg = ConfigService.Load();
        if (ThemeService.FollowsSystem(cfg)) Dispatcher.BeginInvoke(new Action(() => ThemeService.ApplyToApp(cfg)));
    }

    private void RestartDebounce() =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _displayDebounce!.Stop();
            _displayDebounce.Start();
        }));

    /// <param name="cfg">Null = lê do disco. Prévia da tela de Configurações passa um rascunho (cópia) ainda não salvo.</param>
    private void BuildBars(AppConfig? cfg = null)
    {
        CloseBars();

        cfg ??= ConfigService.Load();
        ThemeService.ApplyToApp(cfg);
        Services.Reminders.ReminderStore.Instance.Attach(cfg);
        _barEdge = cfg.Edge;

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

    private DispatcherTimer? _previewDebounce;
    private AppConfig? _pendingPreview;

    /// <summary>
    /// Prévia ao vivo da tela de Configurações, sem salvar. Cores (acento, opacidade) são só
    /// pincéis: aplica na hora. O resto (borda, altura, tema, reservar espaço...) recria as
    /// barras a partir de uma cópia do rascunho, com debounce pra não reconstruir a cada
    /// passo de um slider. Cancelar = <see cref="Reload"/> (volta ao que está no disco).
    /// </summary>
    public void PreviewConfig(AppConfig draft, bool rebuild)
    {
        ThemeService.ApplyToApp(draft);
        if (!rebuild) return;

        _pendingPreview = ConfigService.Clone(draft);
        if (_previewDebounce is null)
        {
            _previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _previewDebounce.Tick += (_, _) =>
            {
                _previewDebounce.Stop();
                if (_pendingPreview is { } pending) BuildBars(pending);
                _pendingPreview = null;
            };
        }
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    /// <summary>Atalho rápido do menu de clique direito — inverte GamingMode e salva na hora, sem precisar abrir Configurações.</summary>
    public void ToggleGamingMode()
    {
        var cfg = ConfigService.Load();
        cfg.GamingMode = !cfg.GamingMode;
        ConfigService.Save(cfg);
        Reload();
    }

    /// <param name="section">Chave da seção a mostrar ("widgets", "aparencia"...); null = a última aberta / padrão.</param>
    public void OpenSettings(string? section = null)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        if (section is not null) _settingsWindow.SelectSection(section);
        BringToFront(_settingsWindow);
    }

    /// <summary>Barra visível em algum monitor (não oculta pelo atalho) — check do "Mostrar barra" da bandeja.</summary>
    public bool AreBarsVisible => _bars.Any(b => b.IsVisible);

    /// <summary>Menu "Posição da barra": troca a borda (e a espessura padrão, se mudou entre horizontal e vertical).</summary>
    public void SetEdge(BarEdge edge)
    {
        var cfg = ConfigService.Load();
        bool wasVertical = cfg.Edge is BarEdge.Left or BarEdge.Right;
        bool vertical = edge is BarEdge.Left or BarEdge.Right;
        cfg.Edge = edge;
        if (wasVertical != vertical) cfg.Thickness = vertical ? 72 : 38;
        ConfigService.Save(cfg);
        Reload();
    }

    /// <summary>Menu "Posição da barra" → "Todos os monitores".</summary>
    public void ShowOnAllMonitors()
    {
        var cfg = ConfigService.Load();
        cfg.MonitorMode = MonitorMode.All;
        ConfigService.Save(cfg);
        Reload();
    }

    /// <summary>
    /// Show() + Activate() sozinhos às vezes não bastam: o clique vem do menu de contexto
    /// da barra, uma janela WS_EX_NOACTIVATE que nunca teve foco de verdade, e o Windows
    /// pode negar a ativação (foreground lock) — a janela nova fica aberta, mas atrás do
    /// app que já estava em uso. Alternar Topmost força o z-order pra cima de qualquer
    /// janela já aberta independente disso, garantindo que apareça na frente.
    /// </summary>
    private static void BringToFront(Window window)
    {
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
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

    /// <summary>Adiciona um widget direto pelo menu de contexto da barra (sem precisar abrir Configurações) — entra no fim da zona Fim.</summary>
    public void AddWidget(string id)
    {
        var cfg = ConfigService.Load();
        cfg.Widgets.End.Add(new WidgetEntry(id));
        ConfigService.Save(cfg);
        Reload();
    }

    // ---------- Avisos de lembrete ----------

    /// <summary>
    /// Agendador único dos lembretes (um alarme só, pro próximo disparo) com o aviso próprio
    /// do Heimdall. Independe do widget de lembretes estar na barra.
    /// </summary>
    private void StartReminderScheduler()
    {
        var store = Services.Reminders.ReminderStore.Instance;
        var toasts = new UI.Reminders.ReminderToastPresenter(store, () => _barEdge, () => OpenReminders());
        _reminderScheduler = new Services.Reminders.ReminderScheduler(store, toasts, new Services.Reminders.DispatcherReminderAlarm());
        _reminderScheduler.Start();

        // Relógio ajustado (fuso, horário de verão manual) e volta da suspensão: recalcula.
        SystemEvents.TimeChanged += OnClockOrPowerChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnClockOrPowerChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() => _reminderScheduler?.Reschedule()));

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) OnClockOrPowerChanged(sender, e);
    }

    /// <summary>
    /// Janela de lembretes — uma só; se já está aberta, vem pra frente. É também onde se
    /// cria e edita lembrete: <paramref name="newReminder"/> abre uma linha nova em edição
    /// no topo, <paramref name="edit"/> põe a linha desse lembrete em edição.
    /// </summary>
    public void OpenReminders(bool newReminder = false, ReminderConfig? edit = null)
    {
        if (_remindersWindow is null)
        {
            _remindersWindow = new RemindersWindow();
            _remindersWindow.Closed += (_, _) => _remindersWindow = null;
            _remindersWindow.Show();
        }
        else if (_remindersWindow.WindowState == WindowState.Minimized)
        {
            _remindersWindow.WindowState = WindowState.Normal;
        }

        BringToFront(_remindersWindow);
        if (edit is not null) _remindersWindow.BeginEdit(edit);
        else if (newReminder) _remindersWindow.BeginNewReminder();
    }

    public void OpenReminderHistory()
    {
        if (_historyWindow is not null)
        {
            BringToFront(_historyWindow);
            return;
        }

        _historyWindow = new ReminderHistoryWindow();
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
        BringToFront(_historyWindow);
    }

    /// <summary>Clique triplo + arrastar na barra: fixa a barra no monitor onde ela foi solta.</summary>
    public void MoveBarToMonitor(string deviceName)
    {
        var cfg = ConfigService.Load();
        cfg.MonitorMode = MonitorMode.Specific;
        cfg.MonitorDevice = MonitorInfo.Normalize(deviceName);
        ConfigService.Save(cfg);
        Reload();
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
        SystemEvents.TimeChanged -= OnClockOrPowerChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _reminderScheduler?.Dispose();
        _updateTimer?.Stop();
        UpdateService.UpdateFound -= ShowUpdateNotification;
        _hotkeys?.Dispose();
        _tray?.Dispose();
        CloseBars();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
