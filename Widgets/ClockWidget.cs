using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Config;

namespace Heimdall.Widgets;

public sealed class ClockWidget : IWidget
{
    private readonly ClockConfig _cfg;
    private readonly DispatcherTimer _timer = new();
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };
    private bool _vertical;

    public FrameworkElement View => _text;

    public ClockWidget(ClockConfig cfg)
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
        var now = DateTime.Now;
        _text.Text = ClockFormatter.Format(_cfg, now, _vertical);

        // Alinha o próximo tick à virada do segundo
        _timer.Interval = TimeSpan.FromMilliseconds(1000 - now.Millisecond + 10);
    }

    public void Dispose() => _timer.Stop();
}
