using System.Windows;
using System.Windows.Controls;
using Heimdall.Config;

namespace Heimdall.Widgets;

/// <summary>Separador visual entre widgets inteiros de uma zona (ex: antes do relógio) — linha 1x18 na cor de borda do tema.</summary>
public sealed class SeparatorWidget : IWidget
{
    private const double Length = 18;

    private readonly Border _line = new()
    {
        Width = 1,
        Height = Length,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(4, 0, 4, 0),
        SnapsToDevicePixels = true
    };

    public FrameworkElement View => _line;

    public SeparatorWidget(AppConfig cfg) => _line.SetResourceReference(Border.BackgroundProperty, "H.Brush.Line");

    public void ApplyOrientation(Orientation orientation)
    {
        // Vertical na barra horizontal, horizontal na barra vertical — sempre cruzando o fluxo dos outros widgets.
        if (orientation == Orientation.Vertical)
        {
            _line.Width = Length;
            _line.Height = 1;
            _line.Margin = new Thickness(0, 4, 0, 4);
        }
    }

    public void Start() { }

    public void Dispose() { }
}
