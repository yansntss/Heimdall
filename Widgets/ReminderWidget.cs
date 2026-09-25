using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI;

namespace Heimdall.Widgets;

/// <summary>
/// Lembretes fixos e agendados como "chips" individuais — hover revela um botão
/// "Concluir" (fade via Opacity, sem deslocar layout), clique direito abre "Excluir".
/// Concluir grava no histórico (<see cref="ReminderHistoryService"/>); excluir não.
/// </summary>
public sealed class ReminderWidget : IWidget
{
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(6);
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphAdd = "";
    private const string GlyphCheck = "";

    /// <summary>Último widget de lembretes construído — é nele que o hotkey global Ctrl+Shift+R abre o popup.</summary>
    internal static ReminderWidget? Primary { get; private set; }

    private readonly AppConfig _cfg;
    private readonly Color _accent;
    private readonly EffectiveStyle _style;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _highlightTimer = new() { Interval = HighlightDuration };
    private readonly Queue<ReminderConfig> _pendingScheduled = new();
    private readonly Dictionary<ReminderConfig, string> _lastFired = new();
    private readonly List<Border> _fixedChips = new();

    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
    private readonly Button _addButton;

    private Border? _activeScheduledChip;
    private Border? _activeBadge;
    private bool _highlighting;
    private QuickAddReminderWindow? _quickAddWindow;

    public FrameworkElement View => _root;

    public ReminderWidget(AppConfig cfg)
    {
        _cfg = cfg;
        _style = ThemeService.GetEffectiveStyle(cfg);
        _accent = _style.Accent;
        _clock.Tick += (_, _) => CheckSchedule();
        _highlightTimer.Tick += (_, _) => { _highlightTimer.Stop(); ShowNextHighlight(); };

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
            ToolTip = "Novo lembrete (Ctrl+Shift+R)"
        };
        _addButton.Click += (_, _) => OpenQuickAdd();

        _root.MouseEnter += (_, _) =>
            _addButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        _root.MouseLeave += (_, _) =>
            _addButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));

        _root.Children.Add(_addButton);

        Primary = this;
    }

    /// <summary>Abre o popup de adição rápida ancorado a esta barra, no lado oposto à borda configurada.</summary>
    public void OpenQuickAdd()
    {
        if (_quickAddWindow is not null)
        {
            _quickAddWindow.Activate();
            return;
        }

        var window = new QuickAddReminderWindow(_style);
        window.AnchorTo(_root, _cfg.Edge);
        window.Saved += reminder =>
        {
            _cfg.Reminders.Add(reminder);
            ConfigService.Save(_cfg);
            RebuildFixedChips();
        };
        window.Closed += (_, _) => _quickAddWindow = null;

        _quickAddWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Editar reutiliza o mesmo popup, pré-preenchido — Saved atualiza o mesmo objeto (mesma referência).</summary>
    private void Edit(ReminderConfig reminder, bool isFixed, Border chip)
    {
        if (_quickAddWindow is not null)
        {
            _quickAddWindow.Activate();
            return;
        }

        var window = new QuickAddReminderWindow(_style, editing: reminder);
        window.AnchorTo(chip, _cfg.Edge);
        window.Saved += _ =>
        {
            ConfigService.Save(_cfg);
            if (isFixed) RebuildFixedChips();
            else RefreshActiveScheduledChip(reminder);
        };
        window.Closed += (_, _) => _quickAddWindow = null;

        _quickAddWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Recria o chip agendado em destaque pra refletir o texto/badge após editar.</summary>
    private void RefreshActiveScheduledChip(ReminderConfig reminder)
    {
        if (_activeScheduledChip is null) return;

        _root.Children.Remove(_activeScheduledChip);
        _activeScheduledChip = CreateChip(reminder, isFixed: false, out _activeBadge);
        _activeScheduledChip.Background = CreatePulsingBrush();
        _root.Children.Insert(0, _activeScheduledChip);
        RefreshBadge();
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start()
    {
        RebuildFixedChips();
        CheckSchedule();
        _clock.Start();
    }

    private void RebuildFixedChips()
    {
        foreach (var chip in _fixedChips) _root.Children.Remove(chip);
        _fixedChips.Clear();

        // O botão "+" fica sempre por último (índice mais alto) — os chips fixos
        // entram antes dele, na ordem em que aparecem no config.
        int insertAt = _root.Children.IndexOf(_addButton);
        foreach (var reminder in _cfg.Reminders.Where(r => r.Kind == ReminderKind.Fixed && !string.IsNullOrWhiteSpace(r.Text)))
        {
            var chip = CreateChip(reminder, isFixed: true, out _);
            _fixedChips.Add(chip);
            _root.Children.Insert(insertAt++, chip);
        }
    }

    private void CheckSchedule()
    {
        var now = DateTime.Now;
        string minuteKey = now.ToString("yyyy-MM-dd HH:mm");

        foreach (var reminder in _cfg.Reminders)
        {
            if (reminder.Kind != ReminderKind.Scheduled) continue;
            if (reminder.Recurrence == ReminderRecurrence.Once && reminder.Completed) continue;
            if (reminder.Recurrence == ReminderRecurrence.Once && reminder.Date is { } date
                && date != DateOnly.FromDateTime(now))
                continue;

            if (!DateTime.TryParseExact(reminder.Time, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time))
                continue;
            if (time.Hour != now.Hour || time.Minute != now.Minute) continue;

            if (reminder.Recurrence == ReminderRecurrence.Weekly && !reminder.Days.Contains(now.DayOfWeek))
                continue;

            var today = DateOnly.FromDateTime(now);
            if (reminder.Recurrence != ReminderRecurrence.Once)
            {
                if (reminder.StartDate is { } start && today < start) continue;
                if (reminder.EndDate is { } end && today > end) continue;
            }

            if (_lastFired.TryGetValue(reminder, out var lastKey) && lastKey == minuteKey) continue;
            _lastFired[reminder] = minuteKey;

            Fire(reminder);
        }
    }

    private void Fire(ReminderConfig reminder)
    {
        _pendingScheduled.Enqueue(reminder);
        // Se outro lembrete já está em destaque, o badge dele precisa refletir esse novo
        // agora — calcular o badge só na criação do chip perderia quem chegou depois
        // (o chip do primeiro já existia quando o segundo foi enfileirado).
        RefreshBadge();
        if (reminder.PlaySound) SystemSounds.Exclamation.Play();

        if (reminder.Recurrence == ReminderRecurrence.Once)
        {
            reminder.Completed = true;
            ConfigService.Save(_cfg);
        }

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

        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    /// <summary>Mostra/atualiza o "+N" no chip agendado ativo conforme quantos ainda estão na fila.</summary>
    private void RefreshBadge()
    {
        if (_activeBadge is null) return;

        int count = _pendingScheduled.Count;
        if (count > 0)
        {
            ((TextBlock)_activeBadge.Child).Text = $"+{count}";
            _activeBadge.ToolTip = $"+{count} lembrete(s) na fila";
            _activeBadge.Visibility = Visibility.Visible;
        }
        else
        {
            _activeBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void DismissActiveScheduled()
    {
        _highlightTimer.Stop();
        ShowNextHighlight();
    }

    // ---------- Concluir / Excluir ----------

    private void Complete(ReminderConfig reminder, bool isFixed, Border chip)
    {
        ReminderHistoryService.AppendCompleted(reminder.Text, reminder.CreatedAt, DateTime.Now);

        if (isFixed)
        {
            _cfg.Reminders.Remove(reminder);
            ConfigService.Save(_cfg);
            _fixedChips.Remove(chip);
            RemoveChipWithAnimation(chip, _root);
        }
        else
        {
            // "Once" já foi marcado Completed no Fire(); recorrentes (Daily/Weekly)
            // continuam no config e disparam de novo no próximo horário.
            DismissActiveScheduled();
        }
    }

    private void Delete(ReminderConfig reminder, bool isFixed, Border chip)
    {
        _cfg.Reminders.Remove(reminder);
        ConfigService.Save(_cfg);

        if (isFixed)
        {
            _fixedChips.Remove(chip);
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

        var text = new TextBlock { Text = reminder.Text, VerticalAlignment = VerticalAlignment.Center };

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
            ToolTip = "Concluir"
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

        var editItem = new MenuItem { Header = "Editar..." };
        editItem.Click += (_, _) => Edit(reminder, isFixed, chip);
        var deleteItem = new MenuItem { Header = "Excluir" };
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
        _highlightTimer.Stop();
        _quickAddWindow?.Close();
        if (Primary == this) Primary = null;
    }
}
