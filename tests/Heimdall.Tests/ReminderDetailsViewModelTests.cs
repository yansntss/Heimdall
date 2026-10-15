using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

public class ReminderDetailsViewModelTests
{
    // Quinta, 15/10/2026, 10:00.
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(-3)));
    private readonly ReminderStore _store;
    private readonly RemindersWindowViewModel _vm;

    public ReminderDetailsViewModelTests()
    {
        _store = new ReminderStore(_ => { }, (_, _, _) => { }, _clock);
        var cfg = new AppConfig();
        cfg.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        cfg.ReminderLists.Add(new ReminderListConfig { Id = "mercado", Name = "Mercado", Color = "#8FD3C8" });
        _store.Attach(cfg);
        _vm = new RemindersWindowViewModel(_store, _ => Brushes.Gray);
    }

    private NewReminderViewModel NewDraft(string title = "")
    {
        _vm.NewReminderCommand.Execute(null);
        var editor = _vm.NewRow!.Editor!;
        editor.Title = title;
        return editor;
    }

    [Fact]
    public void Opens_with_what_the_title_said()
    {
        var editor = NewDraft("Dentista amanhã 14h");
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);

        Assert.True(editor.IsDetailsOpen);
        Assert.True(editor.HasDate);
        Assert.Equal(new DateOnly(2026, 10, 16), editor.Date);
        Assert.True(editor.HasTime);
        Assert.Equal("14:00", editor.TimeText);
    }

    [Fact]
    public void Add_saves_everything_and_strips_the_title()
    {
        var editor = NewDraft("Dentista amanhã 14h");
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);
        editor.Date = new DateOnly(2026, 10, 20);
        editor.StepTimeCommand.Execute("15");
        editor.SelectedPriority = editor.PriorityOptions.Single(p => p.Priority == ReminderPriority.Medium);
        editor.SelectedList = editor.ListOptions.Single(l => l.Id == "mercado");
        editor.Note = "levar exames";
        editor.Flagged = true;

        editor.Details!.ConfirmCommand.Execute(null);

        var r = Assert.Single(_store.Reminders);
        Assert.Equal("Dentista", r.Title);
        Assert.Equal(new DateOnly(2026, 10, 20), r.DueDate);
        Assert.Equal(new TimeOnly(14, 15), r.DueTime);
        Assert.Equal(ReminderPriority.Medium, r.Priority);
        Assert.Equal("mercado", r.ListId);
        Assert.Equal("levar exames", r.Note);
        Assert.True(r.Flagged);
        // "Adicionar" fecha a linha (Enter é que abre outra).
        Assert.Null(_vm.NewRow);
    }

    [Fact]
    public void Cancel_undoes_only_what_was_done_in_the_panel()
    {
        var editor = NewDraft("Ler");
        editor.Flagged = true;
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);
        editor.HasDate = false;
        editor.Flagged = false;
        editor.SelectedPriority = editor.PriorityOptions.Last();

        editor.Details!.CancelCommand.Execute(null);

        Assert.False(editor.IsDetailsOpen);
        Assert.True(editor.Flagged);
        Assert.True(editor.HasDate); // em Hoje o lembrete novo nasce com data de hoje
        Assert.Equal(ReminderPriority.None, editor.SelectedPriority.Priority);
        Assert.Empty(_store.Reminders);
    }

    [Fact]
    public void Escape_goes_back_then_cancels()
    {
        var editor = NewDraft("Ler");
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);
        var details = editor.Details!;

        details.NavigateCommand.Execute(DetailsPage.Repeat);
        Assert.Equal(DetailsPage.Repeat, details.Page);
        Assert.False(details.IsNavigatingBack);
        Assert.Equal("Repetir", details.PageTitle);

        details.EscapeCommand.Execute(null);
        Assert.Equal(DetailsPage.Main, details.Page);
        Assert.True(details.IsNavigatingBack);
        Assert.True(editor.IsDetailsOpen);

        details.EscapeCommand.Execute(null);
        Assert.False(editor.IsDetailsOpen);
    }

    [Fact]
    public void List_chip_opens_straight_on_the_list_page()
    {
        var editor = NewDraft("Leite");
        editor.OpenDetailsCommand.Execute(DetailsPage.List);
        Assert.Equal(DetailsPage.List, editor.Details!.Page);
    }

    [Fact]
    public void Turning_the_date_off_clears_time_and_repeat()
    {
        var editor = NewDraft("Remédio todo dia 21h");
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);
        Assert.Equal(RepeatKind.Daily, editor.SelectedRepeat.Kind);

        editor.HasDate = false;
        editor.Details!.ConfirmCommand.Execute(null);

        var r = Assert.Single(_store.Reminders);
        Assert.Equal("Remédio", r.Title);
        Assert.Null(r.DueDate);
        Assert.Null(r.DueTime);
        Assert.Null(r.Recurrence);
    }

    [Fact]
    public void Time_switch_turns_on_at_the_next_full_hour_and_steps_wrap()
    {
        var editor = NewDraft("Café");
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);
        editor.HasTime = true;
        Assert.Equal("11:00", editor.TimeText);

        editor.TimeText = "23:50";
        editor.StepTimeCommand.Execute("15");
        Assert.Equal("00:05", editor.TimeText);
    }

    [Fact]
    public void Existing_reminder_says_save()
    {
        var r = new ReminderConfig { Title = "Pagar", DueDate = new DateOnly(2026, 10, 15) };
        _store.Add(r);
        _vm.BeginEdit(r);
        var editor = _vm.ActiveEditor!;
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);

        Assert.Equal("Salvar", editor.Details!.ConfirmLabel);
        editor.SelectedPriority = editor.PriorityOptions.Single(p => p.Priority == ReminderPriority.High);
        editor.Details.ConfirmCommand.Execute(null);

        Assert.Equal(ReminderPriority.High, r.Priority);
        Assert.Null(_vm.ActiveEditor);
    }
}
