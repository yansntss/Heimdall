using System.Windows;
using System.Windows.Controls;
using Heimdall.Services;

namespace Heimdall.Widgets;

/// <summary>Marca: a espada Hofuð numa caixa 30x30 com fundo acento @ 14% — clique abre Configurações.</summary>
public sealed class BrandWidget : IWidget
{
    private readonly Border _box = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly FrameworkElement _root;

    public FrameworkElement View => _root;

    public BrandWidget(bool isOverlay = false)
    {
        var icon = WidgetUi.Icon("H.Icon.Sword", 18);
        icon.SetResourceReference(UI.Controls.Icon.ForegroundProperty, "H.Brush.Accent");

        _box.Child = icon;
        _box.SetResourceReference(Border.BackgroundProperty, "H.Brush.AccentWash");
        _box.SetResourceReference(Border.CornerRadiusProperty, "H.Radius.Item");
        _box.SetResourceReference(FrameworkElement.WidthProperty, "H.Size.IconButton");
        _box.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.IconButton");

        if (isOverlay)
        {
            _root = _box;
            return;
        }

        // Botão (não só o Border) pro clique não virar arraste e pra ter nome acessível.
        var button = WidgetUi.IconButton(_box, Strings.BrandTooltip);
        button.Click += (_, _) => ((App)Application.Current).OpenSettings();
        button.MouseEnter += (_, _) => _box.SetResourceReference(Border.BackgroundProperty, "H.Brush.AccentSoft");
        button.MouseLeave += (_, _) => _box.SetResourceReference(Border.BackgroundProperty, "H.Brush.AccentWash");
        _root = button;
    }

    public void ApplyOrientation(Orientation orientation) { }

    public void Start() { }

    public void Dispose() { }
}
