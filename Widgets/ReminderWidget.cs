using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.UI;

namespace Heimdall.Widgets;

/// <summary>
/// Lembretes sem data como "chips" fixos na barra — hover revela um botão "Concluir" (fade
/// via Opacity, sem deslocar layout), clique direito abre Editar/Excluir — e um "+" que
/// cria na janela de lembretes. O aviso na hora marcada é do agendador (toast próprio), não
/// deste widget: funciona mesmo sem ele na barra.
/// </summary>
public sealed class ReminderWidget : IWidget
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphAdd = "";
    private const string GlyphCheck = "";

    private readonly Color _accent;
    private readonly EffectiveStyle _style;

    private readonly List<Border> _fixedChips = new();
    private readonly List<ReminderConfig> _fixedReminders = new();
    private readonly Dictionary<ReminderConfig, string> _fixedTitles = new();
    private readonly ReminderStore _store = ReminderStore.Instance;

    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
    private readonly Button _addButton;

    public FrameworkElement View => _root;

    public ReminderWidget(AppConfig cfg)
    {
        _style = ThemeService.GetEffectiveStyle(cfg);
        _accent = _style.Accent;

        var addIcon = new TextBlock
        {
            Text = GlyphAdd,
            FontFamily = IconFont,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = FrozenBrush(_style.Foreground)
        };
        TextOptions.SetTextRenderingMode(addIcon, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(addIcon, TextFormattingMode.Display);

        _addButton = new Button
        {
            Content = addIcon,
            Opacity = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 4, 0),
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = Strings.ReminderNewTooltip
        };
        System.Windows.Automation.AutomationProperties.SetName(_addButton, Strings.RemindersNew);
        // Criar e editar acontecem na janela de lembretes, com a linha já em edição.
        _addButton.Click += (_, _) => ReminderActions.OpenNew();

        _root.MouseEnter += (_, _) =>
            _addButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        _root.MouseLeave += (_, _) =>
            _addButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));

        _root.Children.Add(_addButton);
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start()
    {
        RebuildFixedChips();
        _store.Changed += RebuildFixedChips;
    }

    /// <summary>
    /// Refaz os chips fixos (lembretes sem data) quando o conjunto muda — criado, concluído
    /// pela lista do sino, ganhou data, renomeado. Igual = não mexe, pra não piscar a cada salvamento.
    /// </summary>
    private void RebuildFixedChips()
    {
        var undated = _store.Undated();
        if (undated.SequenceEqual(_fixedReminders) && undated.All(r => _fixedTitles.TryGetValue(r, out var t) && t == r.Title))
            return;

        foreach (var chip in _fixedChips) _root.Children.Remove(chip);
        _fixedChips.Clear();
        _fixedReminders.Clear();
        _fixedTitles.Clear();

        // O botão "+" fica sempre por último (índice mais alto) — os chips fixos
        // entram antes dele, na ordem em que aparecem no config.
        int insertAt = _root.Children.IndexOf(_addButton);
        foreach (var reminder in undated)
        {
            var chip = CreateChip(reminder);
            _fixedChips.Add(chip);
            _fixedReminders.Add(reminder);
            _fixedTitles[reminder] = reminder.Title;
            _root.Children.Insert(insertAt++, chip);
        }
    }

    // ---------- Concluir / Excluir ----------

    // O chip sai (com animação) antes de avisar o store — o Changed refaz os fixos e
    // tiraria o chip na hora, sem animação.

    private void Complete(ReminderConfig reminder, Border chip)
    {
        RemoveChip(chip);
        _store.Complete(reminder);
    }

    private void Delete(ReminderConfig reminder, Border chip)
    {
        RemoveChip(chip);
        _store.Delete(reminder);
    }

    private void RemoveChip(Border chip)
    {
        int index = _fixedChips.IndexOf(chip);
        if (index >= 0)
        {
            _fixedChips.RemoveAt(index);
            _fixedReminders.RemoveAt(index);
        }

        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        animation.Completed += (_, _) => _root.Children.Remove(chip);
        chip.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private Border CreateChip(ReminderConfig reminder)
    {
        Border chip = null!;

        var text = new TextBlock { Text = reminder.Title, VerticalAlignment = VerticalAlignment.Center };

        var checkIcon = new TextBlock
        {
            Text = GlyphCheck,
            FontFamily = IconFont,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = FrozenBrush(_accent)
        };
        TextOptions.SetTextRenderingMode(checkIcon, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(checkIcon, TextFormattingMode.Display);

        var completeButton = new Button
        {
            Content = checkIcon,
            Opacity = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = Strings.ReminderComplete
        };
        System.Windows.Automation.AutomationProperties.SetName(completeButton, Strings.ReminderCompleteNamed(reminder.Title));
        completeButton.Click += (_, e) => { e.Handled = true; Complete(reminder, chip); };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(text);
        row.Children.Add(completeButton);

        chip = new Border
        {
            Child = row,
            Padding = new Thickness(6, 0, 6, 0),
            // Transparent (não null): Background=null deixa a área "vazia" sem hit-test,
            // então o hover só dispararia em cima do texto, não do chip inteiro.
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        chip.MouseEnter += (_, _) =>
            completeButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        chip.MouseLeave += (_, _) =>
            completeButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));

        var editItem = new MenuItem { Header = Strings.ReminderEdit };
        editItem.Click += (_, _) => ReminderActions.OpenEdit(reminder);
        var deleteItem = new MenuItem { Header = Strings.ReminderDelete };
        deleteItem.Click += (_, _) => Delete(reminder, chip);
        chip.ContextMenu = new ContextMenu { Items = { editItem, deleteItem } };

        return chip;
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public void Dispose() => _store.Changed -= RebuildFixedChips;
}
