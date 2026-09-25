using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;

namespace Heimdall.Widgets;

public sealed class ReminderWidget : IWidget
{
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(6);
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xC1, 0x07));

    private readonly AppConfig _cfg;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _highlightTimer = new() { Interval = HighlightDuration };
    private readonly Queue<string> _pending = new();
    private readonly Dictionary<ReminderConfig, string> _lastFired = new();

    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Background = Brushes.Transparent
    };

    private bool _highlighting;

    public FrameworkElement View => _text;

    public ReminderWidget(AppConfig cfg)
    {
        _cfg = cfg;
        _clock.Tick += (_, _) => CheckSchedule();
        _highlightTimer.Tick += (_, _) => EndHighlight();
    }

    public void ApplyOrientation(Orientation orientation)
    {
        // Texto único; nada a ajustar por orientação.
    }

    public void Start()
    {
        UpdateFixedText();
        CheckSchedule();
        _clock.Start();
    }

    private void UpdateFixedText()
    {
        if (_highlighting) return;

        var joined = string.Join("   •   ", _cfg.Reminders
            .Where(r => r.Kind == ReminderKind.Fixed && !string.IsNullOrWhiteSpace(r.Text))
            .Select(r => r.Text));

        _text.Text = joined;
        _text.Visibility = string.IsNullOrEmpty(joined) ? Visibility.Collapsed : Visibility.Visible;
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
        _pending.Enqueue(reminder.Text);
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
        if (_pending.Count == 0)
        {
            _highlighting = false;
            _text.Background = Brushes.Transparent;
            UpdateFixedText();
            return;
        }

        _highlighting = true;
        _text.Text = _pending.Dequeue();
        _text.Visibility = Visibility.Visible;
        _text.Background = HighlightBrush;
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void EndHighlight()
    {
        _highlightTimer.Stop();
        ShowNextHighlight();
    }

    public void Dispose()
    {
        _clock.Stop();
        _highlightTimer.Stop();
    }
}
