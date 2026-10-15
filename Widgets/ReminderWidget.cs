using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.UI;

namespace Heimdall.Widgets;

/// <summary>
/// Lembretes fixos e agendados como "chips" individuais — hover revela um botão
/// "Concluir" (fade via Opacity, sem deslocar layout), clique direito abre "Excluir".
/// Concluir grava no histórico (<see cref="ReminderHistoryService"/>); excluir não.
/// </summary>
public sealed class ReminderWidget : IWidget
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphAdd = "";
    private const string GlyphCheck = "";

    private readonly bool _isOverlay;
    private readonly Color _accent;
    private readonly EffectiveStyle _style;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Queue<ReminderConfig> _pendingScheduled = new();
    private readonly List<Border> _fixedChips = new();
    private readonly List<ReminderConfig> _fixedReminders = new();
    private readonly Dictionary<ReminderConfig, string> _fixedTitles = new();
    private readonly ReminderStore _store = ReminderStore.Instance;

    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
    private readonly Button _addButton;

    private Border? _activeScheduledChip;
    private Border? _activeBadge;
    private bool _highlighting;

    public FrameworkElement View => _root;

    /// <param name="isOverlay">
    /// Cópia no overlay de tela cheia (janela escondida fora do jogo): não coleta disparos,
    /// que o store entrega uma vez só e a cópia escondida "engoliria".
    /// </param>
    public ReminderWidget(AppConfig cfg, bool isOverlay = false)
    {
        _isOverlay = isOverlay;
        _style = ThemeService.GetEffectiveStyle(cfg);
        _accent = _style.Accent;
        _clock.Tick += (_, _) => CheckSchedule();

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
        CheckSchedule();
        _clock.Start();
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
            var chip = CreateChip(reminder, isFixed: true, out _);
            _fixedChips.Add(chip);
            _fixedReminders.Add(reminder);
            _fixedTitles[reminder] = reminder.Title;
            _root.Children.Insert(insertAt++, chip);
        }
    }

    private void CheckSchedule()
    {
        if (_isOverlay) return;
        foreach (var reminder in _store.CollectDue()) Fire(reminder);
    }

    private void Fire(ReminderConfig reminder)
    {
        _pendingScheduled.Enqueue(reminder);
        // Se outro lembrete já está em destaque, o badge dele precisa refletir esse novo
        // agora — calcular o badge só na criação do chip perderia quem chegou depois
        // (o chip do primeiro já existia quando o segundo foi enfileirado).
        RefreshBadge();
        if (_store.Options.PlaySound) SystemSounds.Exclamation.Play();

        if (!_highlighting) ShowNextHighlight();
    }

    private void ShowNextHighlight()
    {
        if (_activeScheduledChip is not null)
        {
            RemoveChipWithAnimation(_activeScheduledChip, _root);
            _activeScheduledChip = null;
        }

        if (_pendingScheduled.Count == 0)
        {
            _highlighting = false;
            _activeBadge = null;
            return;
        }

        _highlighting = true;
        var reminder = _pendingScheduled.Dequeue();
        _activeScheduledChip = CreateChip(reminder, isFixed: false, out _activeBadge);
        _activeScheduledChip.Background = CreatePulsingBrush();
        _root.Children.Insert(0, _activeScheduledChip);
        RefreshBadge();
    }

    /// <summary>Mostra/atualiza o "+N" no chip agendado ativo conforme quantos ainda estão na fila.</summary>
    private void RefreshBadge()
    {
        if (_activeBadge is null) return;

        int count = _pendingScheduled.Count;
        if (count > 0)
        {
            ((TextBlock)_activeBadge.Child).Text = $"+{count}";
            _activeBadge.ToolTip = Strings.ReminderQueueTooltip(count);
            _activeBadge.Visibility = Visibility.Visible;
        }
        else
        {
            _activeBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void DismissActiveScheduled() => ShowNextHighlight();

    // ---------- Concluir / Excluir ----------

    // O chip sai (com animação) antes de avisar o store — o Changed refaz os fixos e
    // tiraria o chip na hora, sem animação.

    private void Complete(ReminderConfig reminder, bool isFixed, Border chip)
    {
        RemoveChip(isFixed, chip);
        // Recorrente avança pra próxima ocorrência e dispara de novo nela.
        _store.Complete(reminder);
    }

    private void Delete(ReminderConfig reminder, bool isFixed, Border chip)
    {
        RemoveChip(isFixed, chip);
        _store.Delete(reminder);
    }

    private void RemoveChip(bool isFixed, Border chip)
    {
        if (isFixed)
        {
            int index = _fixedChips.IndexOf(chip);
            if (index >= 0)
            {
                _fixedChips.RemoveAt(index);
                _fixedReminders.RemoveAt(index);
            }
            RemoveChipWithAnimation(chip, _root);
        }
        else
        {
            DismissActiveScheduled();
        }
    }

    private static void RemoveChipWithAnimation(Border chip, Panel parent)
    {
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        animation.Completed += (_, _) => parent.Children.Remove(chip);
        chip.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private Border CreateChip(ReminderConfig reminder, bool isFixed, out Border? badge)
    {
        Border chip = null!;
        badge = null;

        var text = new TextBlock { Text = reminder.Title, VerticalAlignment = VerticalAlignment.Center };

        var row = new StackPanel { Orientation = Orientation.Horizontal };

        if (!isFixed)
        {
            var badgeText = new TextBlock
            {
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            badge = new Border
            {
                Background = FrozenBrush(_accent),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Child = badgeText
            };
            row.Children.Add(badge);
        }

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
        completeButton.Click += (_, e) => { e.Handled = true; Complete(reminder, isFixed, chip); };

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
        deleteItem.Click += (_, _) => Delete(reminder, isFixed, chip);
        chip.ContextMenu = new ContextMenu { Items = { editItem, deleteItem } };

        return chip;
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Fundo do chip ao disparar: pulsa com a cor de destaque do tema por alguns segundos.</summary>
    private Brush CreatePulsingBrush()
    {
        var brush = new SolidColorBrush(_accent);
        var animation = new DoubleAnimation
        {
            From = 0.25,
            To = 0.85,
            Duration = TimeSpan.FromMilliseconds(600),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };
        brush.BeginAnimation(Brush.OpacityProperty, animation);
        return brush;
    }

    public void Dispose()
    {
        _clock.Stop();
        _store.Changed -= RebuildFixedChips;
    }
}
