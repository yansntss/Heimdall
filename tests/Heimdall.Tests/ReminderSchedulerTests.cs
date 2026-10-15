using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class ReminderSchedulerTests
{
    private sealed class FakeAlarm : IReminderAlarm
    {
        public TimeSpan? Delay { get; private set; }
        private Action? _callback;

        public void Set(TimeSpan delay, Action callback)
        {
            Delay = delay;
            _callback = callback;
        }

        public void Cancel()
        {
            Delay = null;
            _callback = null;
        }

        public void Ring() => _callback?.Invoke();
    }

    private sealed class FakeNotifier : IReminderNotifier
    {
        public List<string> Shown { get; } = new();
        public bool IsQuiet { get; set; }

        public void Show(ReminderConfig reminder) => Shown.Add(reminder.Title);

        public void ShowGroup(IReadOnlyList<ReminderConfig> reminders, bool overdue) =>
            Shown.Add((overdue ? "atrasados: " : "grupo: ") + string.Join(", ", reminders.Select(r => r.Title)));
    }

    // Quinta, 15/10/2026, 10:00.
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(-3)));
    private readonly ReminderStore _store;
    private readonly FakeAlarm _alarm = new();
    private readonly FakeNotifier _notifier = new();
    private readonly ReminderScheduler _scheduler;

    public ReminderSchedulerTests()
    {
        _store = new ReminderStore(_ => { }, (_, _, _) => { }, _clock);
        var cfg = new AppConfig();
        cfg.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        _store.Attach(cfg);
        _scheduler = new ReminderScheduler(_store, _notifier, _alarm);
    }

    private ReminderConfig Add(string title, int day, int hour, int minute = 0)
    {
        var r = new ReminderConfig { Title = title, DueDate = new DateOnly(2026, 10, day), DueTime = new TimeOnly(hour, minute) };
        _store.Add(r);
        return r;
    }

    private void AdvanceAndRing(TimeSpan by)
    {
        _clock.Now += by;
        _alarm.Ring();
    }

    [Fact]
    public void Overdue_while_closed_become_one_grouped_notice()
    {
        Add("ontem", 14, 9);
        Add("hoje cedo", 15, 8);
        Add("mais tarde", 15, 18);

        _scheduler.Start();

        Assert.Equal(new[] { "atrasados: ontem, hoje cedo" }, _notifier.Shown);
    }

    [Fact]
    public void A_single_overdue_is_a_normal_notice()
    {
        Add("hoje cedo", 15, 8);
        _scheduler.Start();
        Assert.Equal(new[] { "hoje cedo" }, _notifier.Shown);
    }

    [Fact]
    public void Alarm_is_armed_only_for_the_next_one()
    {
        Add("meio-dia", 15, 12);
        Add("tarde", 15, 17);
        _scheduler.Start();

        Assert.Empty(_notifier.Shown);
        Assert.Equal(TimeSpan.FromHours(1), _alarm.Delay); // teto de 1h (o próximo é em 2h)

        AdvanceAndRing(TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromHours(1), _alarm.Delay); // agora falta exatamente 1h
        AdvanceAndRing(TimeSpan.FromHours(1));
        Assert.Equal(new[] { "meio-dia" }, _notifier.Shown);
        Assert.Equal(TimeSpan.FromHours(1), _alarm.Delay);
    }

    [Fact]
    public void Creating_a_reminder_rearms_the_alarm()
    {
        _scheduler.Start();
        Assert.Equal(ReminderScheduler.MaxWait, _alarm.Delay);

        Add("daqui a 10 min", 15, 10, 10);
        Assert.Equal(TimeSpan.FromMinutes(10), _alarm.Delay);

        AdvanceAndRing(TimeSpan.FromMinutes(10));
        Assert.Equal(new[] { "daqui a 10 min" }, _notifier.Shown);
    }

    [Fact]
    public void Same_minute_at_runtime_is_grouped_but_not_called_overdue()
    {
        _scheduler.Start();
        Add("a", 15, 10, 30);
        Add("b", 15, 10, 30);

        AdvanceAndRing(TimeSpan.FromMinutes(30));
        Assert.Equal(new[] { "grupo: a, b" }, _notifier.Shown);
    }

    [Fact]
    public void Snooze_fires_again_after_the_minutes()
    {
        var r = Add("aviso", 15, 10, 5);
        _scheduler.Start();
        AdvanceAndRing(TimeSpan.FromMinutes(5));

        _store.Snooze(r);
        Assert.Equal(TimeSpan.FromMinutes(10), _alarm.Delay);
        AdvanceAndRing(TimeSpan.FromMinutes(10));

        Assert.Equal(new[] { "aviso", "aviso" }, _notifier.Shown);
    }

    [Fact]
    public void Paused_swallows_the_notice()
    {
        Add("aviso", 15, 10, 5);
        _store.Paused = true;
        _scheduler.Start();
        AdvanceAndRing(TimeSpan.FromMinutes(5));

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void Quiet_time_holds_notices_and_releases_them_together()
    {
        _notifier.IsQuiet = true;
        Add("a", 15, 10, 5);
        Add("b", 15, 10, 20);
        _scheduler.Start();

        AdvanceAndRing(TimeSpan.FromMinutes(5));
        Assert.Empty(_notifier.Shown);
        Assert.Equal(ReminderScheduler.QuietRecheck, _alarm.Delay);

        AdvanceAndRing(TimeSpan.FromMinutes(15));
        Assert.Empty(_notifier.Shown);

        _notifier.IsQuiet = false;
        AdvanceAndRing(TimeSpan.FromMinutes(1));
        Assert.Equal(new[] { "grupo: a, b" }, _notifier.Shown);
    }

    [Fact]
    public void Held_notice_completed_meanwhile_is_dropped()
    {
        _notifier.IsQuiet = true;
        var r = Add("a", 15, 10, 5);
        _scheduler.Start();
        AdvanceAndRing(TimeSpan.FromMinutes(5));

        _store.Complete(r);
        _notifier.IsQuiet = false;
        AdvanceAndRing(TimeSpan.FromMinutes(1));

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void Daily_reminder_fires_every_day()
    {
        var r = Add("remédio", 15, 10, 30);
        r.Recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily, Anchor = r.DueDate };
        _store.Update(r);
        _scheduler.Start();

        AdvanceAndRing(TimeSpan.FromMinutes(30));
        Assert.Equal(new[] { "remédio" }, _notifier.Shown);

        // Ninguém concluiu: no dia seguinte avisa de novo.
        for (int i = 0; i < 24; i++) AdvanceAndRing(TimeSpan.FromHours(1));
        Assert.Equal(new[] { "remédio", "remédio" }, _notifier.Shown);
    }
}
