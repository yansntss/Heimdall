using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

/// <summary>Relógio parado num instante, no fuso de São Paulo.</summary>
internal sealed class FixedClock : TimeProvider
{
    private static readonly TimeZoneInfo Sp = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

    public DateTimeOffset Now { get; set; }

    public FixedClock(DateTimeOffset now) => Now = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => Sp;
}

public class ReminderStoreTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(-3)));
    private readonly List<string> _history = new();
    private int _saves;
    private int _changes;
    private readonly ReminderStore _store;

    public ReminderStoreTests()
    {
        _store = new ReminderStore(_ => _saves++, (title, _, _) => _history.Add(title), _clock);
        var cfg = new AppConfig();
        cfg.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        _store.Attach(cfg);
        _store.Changed += () => _changes++;
    }

    private static ReminderConfig At(string title, int day, int hour) =>
        new() { Title = title, DueDate = new DateOnly(2026, 10, day), DueTime = new TimeOnly(hour, 0) };

    [Fact]
    public void Add_saves_notifies_and_fixes_unknown_list()
    {
        var r = new ReminderConfig { Title = "a", ListId = "não existe" };
        _store.Add(r);

        Assert.Equal(1, _saves);
        Assert.Equal(1, _changes);
        Assert.Equal(ReminderListConfig.DefaultId, r.ListId);
        Assert.NotNull(r.CreatedAt);
    }

    [Fact]
    public void Agenda_has_undated_first_then_today_including_overdue()
    {
        _store.Add(At("amanhã", 16, 9));
        _store.Add(At("hoje", 15, 18));
        _store.Add(new ReminderConfig { Title = "fixo" });
        _store.Add(At("ontem", 14, 9));

        Assert.Equal(new[] { "fixo", "ontem", "hoje" }, _store.TodayAgenda().Select(r => r.Title));
        Assert.Equal(3, _store.PendingToday);
    }

    [Fact]
    public void Complete_goes_to_history_and_leaves_the_agenda()
    {
        var r = At("hoje", 15, 18);
        _store.Add(r);
        _store.Complete(r);

        Assert.Equal(new[] { "hoje" }, _history);
        Assert.Empty(_store.TodayAgenda());
    }

    [Fact]
    public void CollectDue_returns_each_occurrence_once()
    {
        _store.Add(At("9h", 15, 9));
        _store.Add(At("11h", 15, 11));

        Assert.Equal(new[] { "9h" }, _store.CollectDue().Select(r => r.Title));
        Assert.Empty(_store.CollectDue());

        _clock.Now = _clock.Now.AddHours(1);
        Assert.Equal(new[] { "11h" }, _store.CollectDue().Select(r => r.Title));
    }

    [Fact]
    public void Paused_swallows_the_occurrence()
    {
        _store.Add(At("9h", 15, 9));
        _store.Paused = true;
        Assert.Empty(_store.CollectDue());

        _store.Paused = false;
        Assert.Empty(_store.CollectDue());
    }

    [Fact]
    public void Snooze_uses_the_configured_minutes()
    {
        var r = At("9h", 15, 9);
        _store.Add(r);
        _store.CollectDue();
        _store.Snooze(r);

        _clock.Now = _clock.Now.AddMinutes(9);
        Assert.Empty(_store.CollectDue());
        _clock.Now = _clock.Now.AddMinutes(1);
        Assert.Single(_store.CollectDue());
    }

    [Fact]
    public void Delete_removes_and_notifies()
    {
        var r = new ReminderConfig { Title = "a" };
        _store.Add(r);
        _store.Delete(r);

        Assert.Empty(_store.Reminders);
        Assert.Equal(2, _changes);
    }
}
