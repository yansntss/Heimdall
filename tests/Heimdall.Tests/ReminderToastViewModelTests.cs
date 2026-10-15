using Heimdall.Config;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

public class ReminderToastViewModelTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 17, 0, 0, TimeSpan.FromHours(-3)));
    private readonly ReminderStore _store;
    private int _opened;

    public ReminderToastViewModelTests()
    {
        _store = new ReminderStore(_ => { }, (_, _, _) => { }, _clock);
        var cfg = new AppConfig();
        cfg.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        cfg.ReminderLists.Add(new ReminderListConfig { Id = "trabalho", Name = "Trabalho" });
        _store.Attach(cfg);
    }

    private ReminderConfig Add(string title, ReminderPriority priority = ReminderPriority.None)
    {
        var r = new ReminderConfig
        {
            Title = title, DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(17, 0),
            ListId = "trabalho", Priority = priority
        };
        _store.Add(r);
        return r;
    }

    [Fact]
    public void Single_notice_texts()
    {
        var vm = new ReminderToastViewModel(Add("Reunião"), _store, () => _opened++);
        Assert.Equal("Reunião", vm.Title);
        Assert.Equal("Hoje, 17:00 · Trabalho", vm.Meta);
        Assert.Equal("Adiar 10 min", vm.PrimaryLabel);
        Assert.Equal("Concluir", vm.SecondaryLabel);
        Assert.True(vm.AutoDismiss);
    }

    [Fact]
    public void Snooze_and_complete_act_and_close()
    {
        var r = Add("Reunião");
        var vm = new ReminderToastViewModel(r, _store, () => _opened++);
        int closed = 0;
        vm.CloseRequested += () => closed++;

        vm.PrimaryCommand.Execute(null);
        Assert.Equal(_clock.Now.AddMinutes(10), r.SnoozedUntil);

        vm.SecondaryCommand.Execute(null);
        Assert.True(r.IsCompleted);
        Assert.Equal(2, closed);
    }

    [Fact]
    public void High_priority_stays_until_someone_acts()
    {
        var vm = new ReminderToastViewModel(Add("Urgente", ReminderPriority.High), _store, () => _opened++);
        Assert.False(vm.AutoDismiss);
    }

    [Fact]
    public void Grouped_overdue_notice()
    {
        var all = new[] { Add("a"), Add("b"), Add("c"), Add("d") };
        var vm = new ReminderToastViewModel(all, overdue: true, _store, () => _opened++);

        Assert.Equal("4 lembretes atrasados", vm.Title);
        Assert.Equal("a · b · c · +1", vm.Meta);
        Assert.Equal("Ver lembretes", vm.PrimaryLabel);

        vm.PrimaryCommand.Execute(null);
        Assert.Equal(1, _opened);
    }
}
