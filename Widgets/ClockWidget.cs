using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using InfoBar.Config;

namespace InfoBar.Widgets;

public sealed class ClockWidget : IWidget
{
    private readonly ClockConfig _cfg;
    private readonly CultureInfo _culture;
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
        try { _culture = CultureInfo.GetCultureInfo(cfg.Culture); }
        catch (CultureNotFoundException) { _culture = CultureInfo.CurrentCulture; }

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

        _text.Text = _vertical
            ? Join("\n", Format(now, _cfg.TimeFormat), Format(now, _cfg.VerticalDateFormat))
            : Join("   ", Format(now, _cfg.DateFormat), Format(now, _cfg.TimeFormat));

        // Alinha o próximo tick à virada do segundo
        _timer.Interval = TimeSpan.FromMilliseconds(1000 - now.Millisecond + 10);
    }

    private string Format(DateTime value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return string.Empty;
        try { return value.ToString(format, _culture); }
        catch (FormatException) { return "?"; }
    }

    private static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(p => p.Length > 0));

    public void Dispose() => _timer.Stop();
}
