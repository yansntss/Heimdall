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
        Assert.Equal(RepeatChoice.Daily, editor.SelectedRepeat.Choice);

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

    // ---------- Repetir ----------

    private NewReminderViewModel OpenRepeat(string title = "Academia")
    {
        var editor = NewDraft(title);
        editor.OpenDetailsCommand.Execute(DetailsPage.Main);
        editor.Details!.NavigateCommand.Execute(DetailsPage.Repeat);
        return editor;
    }

    private static RepeatOption Option(NewReminderViewModel e, RepeatChoice c) => e.RepeatOptions.Single(o => o.Choice == c);

    [Fact]
    public void Repeat_options_are_the_six_of_the_list()
    {
        var editor = OpenRepeat();
        Assert.Equal(new[] { "Nunca", "Diariamente", "Semanalmente", "Dias úteis", "Mensalmente", "Personalizado" },
            editor.RepeatOptions.Select(o => o.Label));
        Assert.Equal(RepeatChoice.Never, editor.SelectedRepeat.Choice);
        Assert.False(editor.IsCustomRepeat);
    }

    [Fact]
    public void Weekdays_is_monday_to_friday()
    {
        var editor = OpenRepeat();
        editor.SelectedRepeat = Option(editor, RepeatChoice.Weekdays);
        editor.Details!.ConfirmCommand.Execute(null);

        var r = Assert.Single(_store.Reminders);
        Assert.Equal(RepeatKind.Weekly, r.Recurrence!.Kind);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            r.Recurrence.Days.OrderBy(d => d));
    }

    [Fact]
    public void Custom_every_two_weeks_on_chosen_days_with_summary()
    {
        var editor = OpenRepeat();
        editor.TimeText = "17:00";
        editor.SelectedRepeat = Option(editor, RepeatChoice.Custom);
        Assert.True(editor.IsCustomRepeat);
        Assert.True(editor.IsWeekUnit);
        // Começa em "toda semana, no dia da data" (quinta).
        Assert.Equal(new[] { DayOfWeek.Thursday }, editor.WeekDayToggles.Where(t => t.IsSelected).Select(t => t.Day));

        editor.IntervalText = "2";
        editor.WeekDayToggles.Single(t => t.Day == DayOfWeek.Thursday).IsSelected = false;
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday })
            editor.WeekDayToggles.Single(t => t.Day == day).IsSelected = true;

        Assert.Equal("A cada 2 semanas, seg · qua · sex, às 17:00", editor.RepeatSummary);
        Assert.Equal("Personalizado", editor.SelectedRepeat.Label);

        editor.Details!.ConfirmCommand.Execute(null);
        var rule = Assert.Single(_store.Reminders).Recurrence!;
        Assert.Equal(2, rule.Interval);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, rule.Days);
    }

    [Fact]
    public void Day_circles_have_full_names_and_start_on_sunday()
    {
        var editor = OpenRepeat();
        Assert.Equal("DSTQQSS", string.Concat(editor.WeekDayToggles.Select(t => t.Letter)));
        Assert.Equal("Segunda", editor.WeekDayToggles[1].Name);
    }

    [Fact]
    public void Changing_the_unit_hides_the_days()
    {
        var editor = OpenRepeat();
        editor.SelectedRepeat = Option(editor, RepeatChoice.Custom);
        editor.SelectedUnit = editor.UnitOptions.Single(u => u.Kind == RepeatKind.Monthly);
        editor.IntervalText = "3";

        Assert.False(editor.IsWeekUnit);
        Assert.Equal("A cada 3 meses, no dia 15", editor.RepeatSummary);
    }

    [Fact]
    public void End_after_n_times_and_on_a_date()
    {
        var editor = OpenRepeat();
        editor.SelectedRepeat = Option(editor, RepeatChoice.Daily);
        editor.Details!.NavigateCommand.Execute(DetailsPage.RepeatEnd);
        Assert.Equal("Terminar", editor.Details.PageTitle);

        editor.SelectedEnd = editor.EndOptions.Single(o => o.End == RepeatEnd.AfterCount);
        editor.EndCountText = "3";
        Assert.True(editor.IsEndAfterCount);
        Assert.Equal("Após 3 vezes", editor.EndText);
        // Terminar faz da regra uma personalizada.
        Assert.Equal(RepeatChoice.Custom, editor.SelectedRepeat.Choice);

        editor.SelectedEnd = editor.EndOptions.Single(o => o.End == RepeatEnd.OnDate);
        editor.EndDate = new DateOnly(2026, 12, 20);
        Assert.Equal("Todo dia, até 20 de dez", editor.RepeatSummary);

        // Esc em "Terminar" volta pra Repetir, não pra principal.
        editor.Details.EscapeCommand.Execute(null);
        Assert.Equal(DetailsPage.Repeat, editor.Details.Page);

        editor.Details.ConfirmCommand.Execute(null);
        var rule = Assert.Single(_store.Reminders).Recurrence!;
        Assert.Equal(new DateOnly(2026, 12, 20), rule.Until);
        Assert.Null(rule.Count);
    }

    [Fact]
    public void Existing_custom_rule_opens_as_custom_and_keeps_its_anchor()
    {
        var r = new ReminderConfig
        {
            Title = "Regar", DueDate = new DateOnly(2026, 10, 15),
            Recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily, Interval = 3, Anchor = new DateOnly(2026, 10, 9) }
        };
        _store.Add(r);
        _vm.BeginEdit(r);
        var editor = _vm.ActiveEditor!;

        Assert.Equal(RepeatChoice.Custom, editor.SelectedRepeat.Choice);
        Assert.Equal("3", editor.IntervalText);

        editor.Flagged = true;
        _vm.CommitActiveEditor();
        Assert.Equal(new DateOnly(2026, 10, 9), r.Recurrence!.Anchor);
        Assert.Equal(3, r.Recurrence.Interval);
    }
}
