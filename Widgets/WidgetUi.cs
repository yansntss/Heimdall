using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Heimdall.UI.Controls;

namespace Heimdall.Widgets;

/// <summary>
/// Peças visuais repetidas entre widgets, todas ligadas aos tokens por
/// SetResourceReference (o equivalente em código de {DynamicResource}) — acompanham
/// troca de tema/acento sem recriar o widget.
/// </summary>
internal static class WidgetUi
{
    public static Geometry Geometry(string key) => (Geometry)Application.Current.FindResource(key);

    public static Icon Icon(string key, double size = 16, bool filled = false) =>
        new() { Geometry = Geometry(key), Size = size, Filled = filled, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Botão só-ícone (30x30 por padrão, raio 8) com nome acessível e tooltip.</summary>
    public static Button IconButton(UIElement content, string name, double? size = null)
    {
        var button = new Button { Content = content, ToolTip = name, Focusable = false };
        button.SetResourceReference(FrameworkElement.StyleProperty, "H.IconButton");
        if (size is { } s)
        {
            button.Width = s;
            button.Height = s;
        }
        AutomationProperties.SetName(button, name);
        return button;
    }

    public static ToggleButton IconToggleButton(UIElement content, string name)
    {
        var button = new ToggleButton { Content = content, ToolTip = name, Focusable = false };
        button.SetResourceReference(FrameworkElement.StyleProperty, "H.IconToggleButton");
        AutomationProperties.SetName(button, name);
        return button;
    }

    public static TextBlock Text(double size, string brushKey = "H.Brush.Text", bool mono = false, FontWeight? weight = null)
    {
        var text = new TextBlock { FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        if (weight is { } w) text.FontWeight = w;
        text.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        if (mono) text.SetResourceReference(TextBlock.FontFamilyProperty, "H.Font.Mono");
        return text;
    }

    /// <summary>Números de status (RAM, temperatura, FPS): mono 11, com respiro lateral.</summary>
    public static void StyleStatusText(TextBlock text)
    {
        text.FontSize = 11;
        // Padding, não Margin: o slot da zona zera a Margin da View.
        text.Padding = new Thickness(6, 0, 6, 0);
        text.SetResourceReference(TextBlock.FontFamilyProperty, "H.Font.Mono");
    }
}
