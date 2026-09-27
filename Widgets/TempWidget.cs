using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.Widgets;

public sealed class TempWidget : IWidget
{
    private readonly TempConfig _cfg;
    private readonly TemperatureService _service = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };
    private bool _vertical;

    // Barra vertical com os dois habilitados: não cabe "CPU 45°C GPU 60°C" — alterna entre
    // os dois a cada tick (3 s) em vez de mostrar os dois juntos espremidos.
    private bool _showCpuNext = true;

    public FrameworkElement View => _text;

    public TempWidget(TempConfig cfg)
    {
        _cfg = cfg;
        _timer.Tick += (_, _) => Update();
    }

    public void ApplyOrientation(Orientation orientation)
    {
        _vertical = orientation == Orientation.Vertical;
        if (_timer.IsEnabled) Update();
    }

    public void Start()
    {
        if (!_service.IsAvailable)
        {
            // Sem admin (ou driver de sensores falhou): mostra "—" uma vez e nem inicia o
            // timer — o nível de elevação não muda em runtime, então tentar de novo a
            // cada 3s não ia mudar nada.
            _text.Text = "—";
            _text.ToolTip = Strings.TempAdminRequiredTooltip;
            return;
        }

        Update();
        _timer.Start();
    }

    private void Update()
    {
        var (cpu, gpu) = _service.ReadTemperatures();
        string cpuText = cpu is { } c ? $"CPU {c:0}°C" : "CPU —";
        string gpuText = gpu is { } g ? $"GPU {g:0}°C" : "GPU —";

        if (_vertical && _cfg.ShowCpu && _cfg.ShowGpu)
        {
            _text.Text = _showCpuNext ? cpuText : gpuText;
            _showCpuNext = !_showCpuNext;
        }
        else
        {
            var parts = new List<string>();
            if (_cfg.ShowCpu) parts.Add(cpuText);
            if (_cfg.ShowGpu) parts.Add(gpuText);
            _text.Text = string.Join("   ", parts);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _service.Dispose();
    }
}
