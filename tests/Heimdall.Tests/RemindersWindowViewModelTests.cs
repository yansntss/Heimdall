using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

public class RemindersWindowViewModelTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(-3)));
    private readonly ReminderStore _store;
    private readonly AppConfig _cfg = new();

    public RemindersWindowViewModelTests()
    {
        _store = new ReminderStore(_ => { }, (_, _, _) => { }, _clock);
        _cfg.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        _cfg.ReminderLists.Add(new ReminderListConfig { Id = "mercado", Name = "Mercado", Color = "#8FD3C8" });
        _store.Attach(_cfg);
    }

    private RemindersWindowViewModel CreateVm() => new(_store, _ => Brushes.Gray);

    private ReminderConfig Add(string title, int? day = 15, int? hour = 9, string list = ReminderListConfig.DefaultId, bool flagged = false)
    {
        var r = new ReminderConfig
        {
            Title = title,
            DueDate = day is { } d ? new DateOnly(2026, 10, d) : null,
            DueTime = hour is { } h ? new TimeOnly(h, 0) : null,
            ListId = list,
            Flagged = flagged
        };
        _store.Add(r);
        return r;
    }

    private static string[] Titles(RemindersWindowViewModel vm) => vm.Items.Select(i => i.Title).ToArray();

    [Fact]
    public void Opens_on_today_with_counts_everywhere()
    {
        Add("hoje");
        Add("amanhã", 16);
        Add("leite", null, null, "mercado", flagged: true);

        var vm = CreateVm();

        Assert.Equal(new[] { "hoje" }, Titles(vm));
        Assert.Equal(new[] { 1, 2, 1, 3 }, vm.SmartLists.Select(s => s.Count));
        Assert.Equal(new[] { 2, 1 }, vm.UserLists.Select(l => l.Count));
    }

    [Fact]
    public void Smart_and_user_list_selection_are_exclusive()
    {
        Add("leite", null, null, "mercado");
        var vm = CreateVm();

        vm.SelectedList = vm.UserLists.Single(l => l.ListId == "mercado");
        Assert.Null(vm.SelectedSmart);
        Assert.Equal("Mercado", vm.Active.Name);
        Assert.Equal(new[] { "leite" }, Titles(vm));

        vm.SelectedSmart = vm.SmartLists[3];
        Assert.Null(vm.SelectedList);
        Assert.Equal(new[] { "leite" }, Titles(vm));
    }

    [Fact]
    public void Completing_the_selected_row_selects_the_next_and_keeps_the_other_rows()
    {
        Add("8h", hour: 8);
        var middle = Add("9h", hour: 9);
        Add("10h", hour: 10);
        var vm = CreateVm();
        var last = vm.Items[2];

        vm.SelectedItem = vm.Items.Single(i => i.Reminder == middle);
        vm.CompleteSelectedCommand.Execute(null);

        Assert.Equal(new[] { "8h", "10h" }, Titles(vm));
        Assert.Same(last, vm.SelectedItem);
        Assert.Same(last, vm.Items[1]);
    }

    [Fact]
    public void Completing_the_last_row_selects_the_new_last()
    {
        Add("8h", hour: 8);
        Add("9h", hour: 9);
        var vm = CreateVm();

        vm.SelectedItem = vm.Items[1];
        vm.CompleteSelectedCommand.Execute(null);

        Assert.Equal("8h", vm.SelectedItem!.Title);
    }

    [Fact]
    public void Editing_updates_the_row_in_place()
    {
        var r = Add("antes");
        var vm = CreateVm();
        var row = vm.Items[0];

        r.Title = "depois";
        _store.Update(r);

        Assert.Same(row, vm.Items[0]);
        Assert.Equal("depois", row.Title);
        Assert.Equal("Concluir lembrete: depois", row.CompleteName);
    }

    [Fact]
    public void Deleting_the_active_list_goes_back_to_today()
    {
        var vm = CreateVm();
        vm.SelectedList = vm.UserLists.Single(l => l.ListId == "mercado");

        _cfg.ReminderLists.RemoveAll(l => l.Id == "mercado");
        _store.Update(new ReminderConfig());

        Assert.Same(vm.SmartLists[0], vm.SelectedSmart);
        Assert.Equal(new[] { ReminderListConfig.DefaultId }, vm.UserLists.Select(l => l.ListId));
    }

    [Fact]
    public void Row_text_shows_date_recurrence_and_list_in_smart_lists()
    {
        var r = Add("remédio", hour: 21, list: "mercado");
        r.Recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily };
        _store.Update(r);
        var vm = CreateVm();

        Assert.Equal("Hoje, 21:00", vm.Items[0].DateText);
        Assert.Equal(" · Todo dia · Mercado", vm.Items[0].ExtraText);
    }

    [Fact]
    public void Overdue_row_is_late_and_can_be_snoozed()
    {
        Add("8h", hour: 8);
        var vm = CreateVm();

        Assert.True(vm.Items[0].IsLate);
        Assert.True(vm.Items[0].CanSnooze);
    }
}
