using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.Widgets;

public sealed class RamWidget : IWidget
{
    private readonly RamConfig _cfg;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };
    private bool _vertical;

    public FrameworkElement View => _text;

    public RamWidget(RamConfig cfg)
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
        Update();
        _timer.Start();
    }

    private void Update()
    {
        var stats = RamStatsService.GetStats();
        if (stats is not { } s)
        {
            _text.Text = "—";
            _text.ToolTip = null;
            return;
        }

        // Barra vertical: só a porcentagem, sem espaço pra "usado / total".
        _text.Text = _vertical || !_cfg.ShowUsedTotal
            ? $"{s.Percent:0}%"
            : $"{s.UsedGb:0.0} / {s.TotalGb:0} GB";
        _text.ToolTip = $"{s.UsedGb:0.0} / {s.TotalGb:0.0} GB ({s.Percent:0}%)";
    }

    public void Dispose() => _timer.Stop();
}
