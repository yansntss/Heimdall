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

namespace Heimdall.Widgets;

/// <summary>
/// Lembretes fixos e agendados como "chips" individuais — hover revela um botão
/// "Concluir" (fade via Opacity, sem deslocar layout), clique direito abre "Excluir".
/// Concluir grava no histórico (<see cref="ReminderHistoryService"/>); excluir não.
/// </summary>
public sealed class ReminderWidget : IWidget
{
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(6);
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xC1, 0x07));
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphCheck = "";

    private readonly AppConfig _cfg;
    private readonly Color _accent;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _highlightTimer = new() { Interval = HighlightDuration };
    private readonly Queue<ReminderConfig> _pendingScheduled = new();
    private readonly Dictionary<ReminderConfig, string> _lastFired = new();
    private readonly List<Border> _fixedChips = new();

    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center };

    private Border? _activeScheduledChip;
    private bool _highlighting;

    public FrameworkElement View => _root;

    public ReminderWidget(AppConfig cfg)
    {
        _cfg = cfg;
        _accent = ThemeService.GetEffectiveStyle(cfg).Accent;
        _clock.Tick += (_, _) => CheckSchedule();
        _highlightTimer.Tick += (_, _) => { _highlightTimer.Stop(); ShowNextHighlight(); };
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

        foreach (var reminder in _cfg.Reminders.Where(r => r.Kind == ReminderKind.Fixed && !string.IsNullOrWhiteSpace(r.Text)))
        {
            var chip = CreateChip(reminder, isFixed: true);
            _fixedChips.Add(chip);
            _root.Children.Add(chip);
        }

        _root.Visibility = _fixedChips.Count > 0 || _activeScheduledChip is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CheckSchedule()
    {
        var now = DateTime.Now;
        string minuteKey = now.ToString("yyyy-MM-dd HH:mm");

        foreach (var reminder in _cfg.Reminders)
        {
            if (reminder.Kind != ReminderKind.Scheduled) continue;
            if (reminder.Recurrence == ReminderRecurrence.Once && reminder.Completed) continue;

            if (!DateTime.TryParseExact(reminder.Time, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time))
                continue;
            if (time.Hour != now.Hour || time.Minute != now.Minute) continue;

            if (reminder.Recurrence == ReminderRecurrence.Weekly && !reminder.Days.Contains(now.DayOfWeek))
                continue;

            if (_lastFired.TryGetValue(reminder, out var lastKey) && lastKey == minuteKey) continue;
            _lastFired[reminder] = minuteKey;

            Fire(reminder);
        }
    }

    private void Fire(ReminderConfig reminder)
    {
        _pendingScheduled.Enqueue(reminder);
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
            _root.Visibility = _fixedChips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        _highlighting = true;
        var reminder = _pendingScheduled.Dequeue();
        _activeScheduledChip = CreateChip(reminder, isFixed: false);
        _activeScheduledChip.Background = HighlightBrush;
        _root.Children.Insert(0, _activeScheduledChip);
        _root.Visibility = Visibility.Visible;

        _highlightTimer.Stop();
        _highlightTimer.Start();
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

    private Border CreateChip(ReminderConfig reminder, bool isFixed)
    {
        Border chip = null!;

        var text = new TextBlock { Text = reminder.Text, VerticalAlignment = VerticalAlignment.Center };

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

        var deleteItem = new MenuItem { Header = "Excluir" };
        deleteItem.Click += (_, _) => Delete(reminder, isFixed, chip);
        chip.ContextMenu = new ContextMenu { Items = { deleteItem } };

        return chip;
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        _clock.Stop();
        _highlightTimer.Stop();
    }
}
