using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

public class RemindersWindowViewModelTests
{
    // Quinta, 15/10/2026, 10:00.
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

    private static List<ReminderRowViewModel> Rows(RemindersWindowViewModel vm) => vm.Items.OfType<ReminderRowViewModel>().ToList();

    private static string[] Titles(RemindersWindowViewModel vm) => Rows(vm).Select(i => i.Title).ToArray();

    /// <summary>Cabeçalhos como "§TÍTULO", lembretes pelo título — a lista inteira na ordem.</summary>
    private static string[] Layout(RemindersWindowViewModel vm) => vm.Items.Select(i => i switch
    {
        ReminderSectionViewModel s => "§" + s.Title,
        ReminderRowViewModel r => r.Title,
        _ => "?"
    }).ToArray();

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
        var last = Rows(vm)[2];

        vm.SelectedItem = Rows(vm).Single(i => i.Reminder == middle);
        vm.CompleteSelectedCommand.Execute(null);

        Assert.Equal(new[] { "8h", "10h" }, Titles(vm));
        Assert.Same(last, vm.SelectedItem);
        Assert.Same(last, Rows(vm)[1]);
    }

    [Fact]
    public void Completing_the_last_row_selects_the_new_last()
    {
        Add("8h", hour: 8);
        Add("9h", hour: 9);
        var vm = CreateVm();

        vm.SelectedItem = Rows(vm)[1];
        vm.CompleteSelectedCommand.Execute(null);

        Assert.Equal("8h", ((ReminderRowViewModel)vm.SelectedItem!).Title);
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
        r.Note = "  depois do jantar ";
        _store.Update(r);
        var vm = CreateVm();
        var row = Rows(vm)[0];

        Assert.Equal("Hoje, 21:00", row.DateText);
        Assert.Equal(" · Todo dia · Mercado", row.ExtraText);
        Assert.Equal("depois do jantar", row.Note);
    }

    [Fact]
    public void Overdue_row_is_late_and_can_be_snoozed()
    {
        Add("8h", hour: 8);
        var vm = CreateVm();

        Assert.True(Rows(vm)[0].IsLate);
        Assert.True(Rows(vm)[0].CanSnooze);
    }

    // ---------- Seções ----------

    [Fact]
    public void Today_has_sections_only_when_something_is_overdue()
    {
        Add("hoje");
        var vm = CreateVm();
        Assert.Equal(new[] { "hoje" }, Layout(vm));

        Add("ontem", 14);
        Assert.Equal(new[] { "§ATRASADOS", "ontem", "§HOJE", "hoje" }, Layout(vm));
    }

    [Fact]
    public void Scheduled_groups_by_day()
    {
        Add("ontem", 14);
        Add("hoje", 15);
        Add("amanhã", 16);
        Add("sábado", 17);
        Add("longe", 30);
        var vm = CreateVm();
        vm.SelectedSmart = vm.SmartLists[1];

        Assert.Equal(new[] { "§ATRASADOS", "ontem", "§HOJE", "hoje", "§AMANHÃ", "amanhã", "§SÁBADO", "sábado", "§30 OUT", "longe" }, Layout(vm));
    }

    [Fact]
    public void All_groups_by_list_in_list_order()
    {
        Add("leite", null, null, "mercado");
        Add("relatório", 20);
        Add("pão", 15, 8, "mercado");
        var vm = CreateVm();
        vm.SelectedSmart = vm.SmartLists[3];

        Assert.Equal(new[] { "§LEMBRETES", "relatório", "§MERCADO", "pão", "leite" }, Layout(vm));
    }

    [Fact]
    public void Section_headers_are_never_selected()
    {
        Add("ontem", 14);
        var vm = CreateVm();
        var header = vm.Items.OfType<ReminderSectionViewModel>().First();

        vm.SelectedItem = header;
        Assert.Null(vm.SelectedItem);
    }

    // ---------- Lembrete novo ----------

    [Fact]
    public void New_reminder_in_today_is_dated_today_and_enter_opens_another()
    {
        var vm = CreateVm();
        vm.NewReminderCommand.Execute(null);
        var draft = vm.Draft!;
        Assert.Same(draft, vm.ActiveEditor);

        draft.Title = "Comprar pão";
        draft.SubmitCommand.Execute(null);

        var created = Assert.Single(_store.Reminders);
        Assert.Equal("Comprar pão", created.Title);
        Assert.Equal(new DateOnly(2026, 10, 15), created.DueDate);
        Assert.Null(created.DueTime);
        Assert.Equal(new[] { "Comprar pão" }, Titles(vm));

        // Enter abre outra linha em branco, pronta pra digitar.
        Assert.NotNull(vm.Draft);
        Assert.NotSame(draft, vm.Draft);
        Assert.True(vm.Draft!.IsBlank);
    }

    [Fact]
    public void New_reminder_takes_date_from_the_title()
    {
        var vm = CreateVm();
        vm.NewReminderCommand.Execute(null);
        vm.Draft!.Title = "Dentista amanhã 14h";
        Assert.Equal("Amanhã, 14:00", vm.Draft.DateChipText);
        Assert.True(vm.Draft.IsDateFromTitle);

        vm.Draft.SubmitCommand.Execute(null);

        var created = Assert.Single(_store.Reminders);
        Assert.Equal("Dentista", created.Title);
        Assert.Equal(new DateOnly(2026, 10, 16), created.DueDate);
        Assert.Equal(new TimeOnly(14, 0), created.DueTime);
    }

    [Fact]
    public void New_reminder_with_recurrence_from_the_title()
    {
        var vm = CreateVm();
        vm.NewReminderCommand.Execute(null);
        vm.Draft!.Title = "Academia toda segunda e quarta 8h";
        vm.Draft.SubmitCommand.Execute(null);

        var created = Assert.Single(_store.Reminders);
        Assert.Equal("Academia", created.Title);
        Assert.Equal(RepeatKind.Weekly, created.Recurrence!.Kind);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, created.Recurrence.Days);
    }

    [Fact]
    public void New_reminder_in_a_user_list_or_flagged_gets_their_defaults()
    {
        var vm = CreateVm();
        vm.SelectedList = vm.UserLists.Single(l => l.ListId == "mercado");
        vm.NewReminderCommand.Execute(null);
        vm.Draft!.Title = "Leite";
        vm.Draft.SubmitCommand.Execute(null);

        vm.SelectedSmart = vm.SmartLists[2];
        vm.NewReminderCommand.Execute(null);
        vm.Draft!.Title = "Importante";
        vm.Draft.SubmitCommand.Execute(null);

        Assert.Equal("mercado", _store.Reminders.Single(r => r.Title == "Leite").ListId);
        Assert.Null(_store.Reminders.Single(r => r.Title == "Leite").DueDate);
        Assert.True(_store.Reminders.Single(r => r.Title == "Importante").Flagged);
    }

    [Fact]
    public void Clicking_outside_saves_the_draft_and_closes_it()
    {
        var vm = CreateVm();
        vm.NewReminderCommand.Execute(null);
        vm.Draft!.Title = "Ligar pro banco";

        vm.CommitActiveEditor();

        Assert.Single(_store.Reminders);
        Assert.Null(vm.Draft);
        Assert.Null(vm.ActiveEditor);
    }

    [Fact]
    public void Escape_and_blank_drafts_add_nothing()
    {
        var vm = CreateVm();
        vm.NewReminderCommand.Execute(null);
        vm.Draft!.Title = "Esquece";
        vm.Draft.CancelCommand.Execute(null);
        Assert.Null(vm.Draft);

        vm.NewReminderCommand.Execute(null);
        vm.Draft!.SubmitCommand.Execute(null);

        Assert.Empty(_store.Reminders);
        Assert.Null(vm.Draft);
    }

    [Fact]
    public void Chosen_date_wins_over_the_title_and_keeps_the_text()
    {
        var vm = CreateVm();
        vm.NewReminderCommand.Execute(null);
        var draft = vm.Draft!;
        draft.Title = "Ler capítulo 14h";
        draft.QuickDateCommand.Execute("tomorrow");
        draft.TimeText = "20:00";
        draft.SubmitCommand.Execute(null);

        var created = Assert.Single(_store.Reminders);
        Assert.Equal(new DateOnly(2026, 10, 16), created.DueDate);
        Assert.Equal(new TimeOnly(20, 0), created.DueTime);
    }

    // ---------- Edição de linha ----------

    [Fact]
    public void Editing_a_row_in_place()
    {
        var r = Add("antes");
        var vm = CreateVm();
        var row = Rows(vm)[0];

        row.BeginEditCommand.Execute(null);
        Assert.True(row.IsEditing);
        Assert.Same(row.Editor, vm.ActiveEditor);
        Assert.False(vm.CompleteSelectedCommand.CanExecute(null)); // Espaço digita, não conclui

        row.Editor!.Title = "depois";
        row.Editor.ToggleFlagCommand.Execute(null);
        row.Editor.ChooseListCommand.Execute("mercado");
        vm.CommitActiveEditor();

        Assert.False(row.IsEditing);
        Assert.Same(row, Rows(vm)[0]);
        Assert.Equal("depois", r.Title);
        Assert.True(r.Flagged);
        Assert.Equal("mercado", r.ListId);
        Assert.Equal("Concluir lembrete: depois", row.CompleteName);
        Assert.Equal(new DateOnly(2026, 10, 15), r.DueDate);
    }

    [Fact]
    public void Enter_on_a_row_saves_and_opens_a_new_line()
    {
        Add("antes");
        var vm = CreateVm();
        var row = Rows(vm)[0];
        row.BeginEditCommand.Execute(null);
        row.Editor!.Title = "depois";

        row.Editor.SubmitCommand.Execute(null);

        Assert.Equal("depois", row.Title);
        Assert.NotNull(vm.Draft);
        Assert.Same(vm.Draft, vm.ActiveEditor);
    }

    [Fact]
    public void Escape_on_a_row_discards_changes()
    {
        var r = Add("antes");
        var vm = CreateVm();
        var row = Rows(vm)[0];
        row.BeginEditCommand.Execute(null);
        row.Editor!.Title = "depois";

        row.Editor.CancelCommand.Execute(null);

        Assert.Equal("antes", r.Title);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public void Clearing_the_title_deletes_the_reminder()
    {
        Add("apagar");
        var vm = CreateVm();
        var row = Rows(vm)[0];
        row.BeginEditCommand.Execute(null);
        row.Editor!.Title = "  ";

        vm.CommitActiveEditor();

        Assert.Empty(_store.Reminders);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Editing_another_row_saves_the_first()
    {
        var first = Add("um", hour: 8);
        Add("dois", hour: 9);
        var vm = CreateVm();
        Rows(vm)[0].BeginEditCommand.Execute(null);
        Rows(vm)[0].Editor!.Title = "um!";

        Rows(vm)[1].BeginEditCommand.Execute(null);

        Assert.Equal("um!", first.Title);
        Assert.Single(Rows(vm), r => r.IsEditing);
        Assert.True(Rows(vm)[1].IsEditing);
    }

    [Fact]
    public void Untouched_title_with_a_date_word_is_not_reparsed()
    {
        var r = Add("Reunião de sexta");
        var vm = CreateVm();
        var row = Rows(vm)[0];
        row.BeginEditCommand.Execute(null);
        row.Editor!.ToggleFlagCommand.Execute(null);
        vm.CommitActiveEditor();

        Assert.Equal("Reunião de sexta", r.Title);
        Assert.Equal(new DateOnly(2026, 10, 15), r.DueDate);
    }

    [Fact]
    public void Changing_the_schedule_rearms_the_notification()
    {
        var r = Add("aviso", hour: 8);
        r.NotifiedAt = _clock.Now;
        var vm = CreateVm();
        var row = Rows(vm)[0];
        row.BeginEditCommand.Execute(null);
        row.Editor!.TimeText = "18:00";
        vm.CommitActiveEditor();

        Assert.Equal(new TimeOnly(18, 0), r.DueTime);
        Assert.Null(r.NotifiedAt);
    }

    [Fact]
    public void Repeat_chosen_in_the_popup()
    {
        var r = Add("regar", hour: 7);
        var vm = CreateVm();
        var row = Rows(vm)[0];
        row.BeginEditCommand.Execute(null);
        row.Editor!.SelectedRepeat = row.Editor.RepeatOptions.Single(o => o.Kind == RepeatKind.Weekly);
        vm.CommitActiveEditor();

        Assert.Equal(RepeatKind.Weekly, r.Recurrence!.Kind);
        Assert.Equal(new[] { DayOfWeek.Thursday }, r.Recurrence.Days);
    }
}
