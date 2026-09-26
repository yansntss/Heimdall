using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.Widgets;

/// <summary>Separador visual entre widgets inteiros de uma zona (ex: entre "clock" e "media") — sem config própria, só uma linha.</summary>
public sealed class SeparatorWidget : IWidget
{
    private readonly Border _line;

    public FrameworkElement View => _line;

    public SeparatorWidget(AppConfig cfg)
    {
        var style = ThemeService.GetEffectiveStyle(cfg);
        var brush = new SolidColorBrush(style.Border);
        brush.Freeze();

        double thickness = Math.Max(1, cfg.Thickness * 0.6);
        _line = new Border
        {
            Background = brush,
            Width = 1,
            Height = thickness,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        };
    }

    public void ApplyOrientation(Orientation orientation)
    {
        // Vertical na barra horizontal, horizontal na barra vertical — sempre cruzando o fluxo dos outros widgets.
        if (orientation == Orientation.Vertical)
        {
            _line.Width = _line.Height;
            _line.Height = 1;
            _line.Margin = new Thickness(0, 4, 0, 4);
        }
    }

    public void Start() { }

    public void Dispose() { }
}
