using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.UI;

/// <summary>
/// Janela de lembretes (420px, redimensionável). Lógica toda no <see cref="RemindersWindowViewModel"/>;
/// aqui só o chrome próprio e manter o foco do teclado na linha selecionada.
/// </summary>
public partial class RemindersWindow : Window
{
    private readonly RemindersWindowViewModel _vm;

    public RemindersWindow()
    {
        InitializeComponent();
        Title = Strings.RemindersWindowTitle;
        _vm = new RemindersWindowViewModel(ReminderStore.Instance, key => (Brush)FindResource(key));
        DataContext = _vm;

        StateChanged += (_, _) => ApplyWindowState();
        ItemsList.SelectionChanged += (_, _) => KeepKeyboardFocusOnSelection();
        PreviewMouseDown += CommitEditOnOutsideClick;
        // Trocar de janela salva a edição em andamento (os popups de data/lista não ativam
        // janela própria, então não caem aqui).
        Deactivated += (_, _) => _vm.CommitActiveEditor();
        Closing += (_, _) => _vm.CommitActiveEditor();
        Closed += (_, _) => _vm.Dispose();
    }

    /// <summary>
    /// "Clicar fora salva": clique em qualquer lugar da janela que não seja a edição aberta
    /// (os popups de data e lista são outra janela e nem passam por aqui).
    /// </summary>
    private void CommitEditOnOutsideClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.ActiveEditor is not { } editor) return;

        for (var node = e.OriginalSource as DependencyObject; node is not null; node = ParentOf(node))
            if (node is FrameworkElement { DataContext: var context } && ReferenceEquals(context, editor)) return;

        _vm.CommitActiveEditor();
    }

    /// <summary>Sobe pela árvore visual; Run e outros elementos de texto só têm pai lógico.</summary>
    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

    // ---------- Chrome (só visual) ----------

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Maximizada, compensa a borda de redimensionar que passa da tela e tira o raio.</summary>
    private void ApplyWindowState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        var border = SystemParameters.WindowResizeBorderThickness;
        Frame.Margin = maximized ? new Thickness(border.Left + 1, border.Top + 1, border.Right + 1, border.Bottom + 1) : new Thickness(0);
        Frame.CornerRadius = new CornerRadius(maximized ? 0 : 16);
        MaximizeIcon.Geometry = (Geometry)FindResource(maximized ? "H.Icon.Restore" : "H.Icon.Maximize");
    }

    /// <summary>
    /// Concluir com Espaço remove a linha focada; o ViewModel passa a seleção pra seguinte,
    /// e aqui o foco do teclado vai junto — senão o próximo Espaço não chegaria na lista.
    /// </summary>
    private void KeepKeyboardFocusOnSelection()
    {
        if (!ItemsList.IsKeyboardFocusWithin && Keyboard.FocusedElement is not null and not Window) return;
        if (ItemsList.SelectedItem is not { } item) return;

        Dispatcher.BeginInvoke(() =>
        {
            ItemsList.ScrollIntoView(item);
            if (ItemsList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container) container.Focus();
        });
    }
}
