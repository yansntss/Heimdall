using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>Lista os apps instalados (Store/MSIX) com busca — escolher um devolve o AppUserModelId via <see cref="Chosen"/>.</summary>
internal sealed class InstalledAppPickerWindow : Window
{
    private readonly List<InstalledAppsService.InstalledApp> _all;
    private readonly TextBox _search;
    private readonly ListBox _list;

    public event Action<InstalledAppsService.InstalledApp>? Chosen;

    public InstalledAppPickerWindow(EffectiveStyle style)
    {
        _all = InstalledAppsService.GetAll();

        Title = "Heimdall — Escolher app instalado";
        Width = 360;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        _search = new TextBox { Margin = new Thickness(8, 8, 8, 4), Padding = new Thickness(4) };
        _search.TextChanged += (_, _) => Filter();

        _list = new ListBox { Margin = new Thickness(8, 0, 8, 8), DisplayMemberPath = nameof(InstalledAppsService.InstalledApp.Name) };
        _list.MouseDoubleClick += (_, _) => Choose();

        var okButton = new Button { Content = "Adicionar", Width = 90, Margin = new Thickness(0, 0, 8, 8), HorizontalAlignment = HorizontalAlignment.Right };
        okButton.Click += (_, _) => Choose();

        var root = new DockPanel();
        DockPanel.SetDock(_search, Dock.Top);
        DockPanel.SetDock(okButton, Dock.Bottom);
        root.Children.Add(_search);
        root.Children.Add(okButton);
        root.Children.Add(_list);
        Content = root;

        Background = new SolidColorBrush(style.Background);
        Foreground = new SolidColorBrush(style.Foreground);

        Loaded += (_, _) => { Filter(); _search.Focus(); };
    }

    private void Filter()
    {
        string term = _search.Text.Trim();
        _list.ItemsSource = string.IsNullOrEmpty(term)
            ? _all
            : _all.Where(a => a.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Choose()
    {
        if (_list.SelectedItem is InstalledAppsService.InstalledApp app)
        {
            Chosen?.Invoke(app);
            Close();
        }
    }
}
