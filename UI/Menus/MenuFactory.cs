using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Heimdall.UI.Controls;

namespace Heimdall.UI.Menus;

/// <summary>
/// Monta itens de menu no padrão do Themes/Menus.xaml (ícone 14 à esquerda, atalho à
/// direita, destrutivo em vermelho). A ação roda depois do menu fechar — várias delas
/// recriam a barra, que é dona do menu.
/// </summary>
internal static class MenuFactory
{
    public static MenuItem Item(string header, string? iconKey = null, Action? onClick = null,
        string? gesture = null, bool danger = false, bool? isChecked = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
        if (iconKey is not null) item.Icon = Icon(iconKey);
        if (danger) item.SetResourceReference(FrameworkElement.StyleProperty, "H.MenuItem.Danger");
        if (isChecked is { } check) item.IsChecked = check;
        AutomationProperties.SetName(item, header);
        if (onClick is not null)
            item.Click += (_, _) => Application.Current.Dispatcher.BeginInvoke(onClick);
        return item;
    }

    public static MenuItem Submenu(string header, string? iconKey, IEnumerable<object> children)
    {
        var item = Item(header, iconKey);
        foreach (var child in children) item.Items.Add(child);
        return item;
    }

    public static MenuItem Caption(string text)
    {
        var item = new MenuItem { Header = text };
        item.SetResourceReference(FrameworkElement.StyleProperty, "H.MenuItem.Caption");
        return item;
    }

    public static Separator Separator() => new();

    private static Icon Icon(string key) => new()
    {
        Geometry = (Geometry)Application.Current.FindResource(key),
        Size = 14,
        VerticalAlignment = VerticalAlignment.Center
    };
}
