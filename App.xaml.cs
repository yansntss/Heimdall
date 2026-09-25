using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using InfoBar.Config;
using InfoBar.Services;
using InfoBar.UI;
using Microsoft.Win32;

namespace InfoBar;

public partial class App : Application
{
    private readonly List<BarWindow> _bars = new();
    private Mutex? _mutex;
    private DispatcherTimer? _displayDebounce;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "InfoBar.SingleInstance", out bool created);
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

        BuildBars();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
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
            var bar = new BarWindow(cfg, monitor);
            _bars.Add(bar);
            bar.Show();
        }
    }

    private void CloseBars()
    {
        foreach (var bar in _bars) bar.Close();
        _bars.Clear();
    }

    // ---- Ações chamadas pelo menu de contexto da barra ----

    public void Reload() => BuildBars();

    public void OpenConfig()
    {
        ConfigService.EnsureExists();
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{ConfigService.ConfigPath}\"") { UseShellExecute = true });
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
            "InfoBar — Monitores");
    }

    public void ExitApp()
    {
        CloseBars();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        CloseBars();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
