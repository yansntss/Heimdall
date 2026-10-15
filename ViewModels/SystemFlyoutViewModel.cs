using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI.Popups;

namespace Heimdall.ViewModels;

/// <summary>Dropdown do chip de CPU: medidores de CPU/RAM/disco, tempo ligado e ações rápidas.</summary>
internal sealed class SystemFlyoutViewModel : ObservableObject, IDisposable
{
    private readonly AppConfig _cfg;
    private readonly CpuUsageSampler _cpu = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private double _cpuPercent, _ramPercent, _diskPercent;
    private string _cpuText = "—", _ramText = "—", _diskText = "—", _uptimeText = "";

    public SystemFlyoutViewModel(AppConfig cfg)
    {
        _cfg = cfg;
        HideBarCommand = new RelayCommand(() =>
        {
            FlyoutWindow.CloseCurrent();
            ((App)Application.Current).ToggleBarsVisibility();
        });
        SettingsCommand = new RelayCommand(() =>
        {
            FlyoutWindow.CloseCurrent();
            ((App)Application.Current).OpenSettings();
        });

        _cpu.Sample(); // arma a primeira diferença
        UpdateDisk();
        Update();
        _timer.Tick += (_, _) => Update();
        _timer.Start();
    }

    public double CpuPercent { get => _cpuPercent; private set => SetProperty(ref _cpuPercent, value); }
    public string CpuText { get => _cpuText; private set => SetProperty(ref _cpuText, value); }
    public double RamPercent { get => _ramPercent; private set => SetProperty(ref _ramPercent, value); }
    public string RamText { get => _ramText; private set => SetProperty(ref _ramText, value); }
    public double DiskPercent { get => _diskPercent; private set => SetProperty(ref _diskPercent, value); }
    public string DiskText { get => _diskText; private set => SetProperty(ref _diskText, value); }
    public string UptimeText { get => _uptimeText; private set => SetProperty(ref _uptimeText, value); }

    /// <summary>
    /// Mesmo AppConfig que o BarPresenter consulta ao detectar tela cheia — basta mudar e
    /// salvar, sem recriar a barra.
    /// </summary>
    public bool OverlayEnabled
    {
        get => _cfg.GamingMode;
        set
        {
            if (_cfg.GamingMode == value) return;
            _cfg.GamingMode = value;
            ConfigService.Save(_cfg);
            OnPropertyChanged();
        }
    }

    public ICommand HideBarCommand { get; }
    public ICommand SettingsCommand { get; }

    private void Update()
    {
        if (_cpu.Sample() is { } cpu)
        {
            CpuPercent = cpu;
            CpuText = $"{cpu:0}%";
        }

        if (RamStatsService.GetStats() is { } ram)
        {
            RamPercent = ram.Percent;
            RamText = $"{ram.UsedGb:0.0} / {ram.TotalGb:0} GB";
        }

        UptimeText = Strings.SystemUptime(TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    /// <summary>Unidade do Windows — só na abertura, muda devagar.</summary>
    private void UpdateDisk()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            const double gb = 1024.0 * 1024 * 1024;
            double total = drive.TotalSize / gb, used = (drive.TotalSize - drive.TotalFreeSpace) / gb;
            DiskPercent = total > 0 ? used / total * 100 : 0;
            DiskText = $"{used:0} / {total:0} GB";
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() => _timer.Stop();
}
