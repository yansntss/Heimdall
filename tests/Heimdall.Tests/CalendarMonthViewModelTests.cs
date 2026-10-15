using System.Globalization;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

public class CalendarMonthViewModelTests
{
    private static readonly CultureInfo Pt = new("pt-BR");
    // Quinta, 15/10/2026.
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static CalendarMonthViewModel Create(DayOfWeek first = DayOfWeek.Sunday) => new(Today, first, Pt);

    [Fact]
    public void Opens_on_the_current_month_with_six_full_weeks()
    {
        var vm = Create();
        Assert.Equal("Outubro 2026", vm.Title);
        Assert.Equal(42, vm.Days.Count);
        // 01/10/2026 é quinta: com a semana começando no domingo, a grade começa em 27/09.
        Assert.Equal(new DateOnly(2026, 9, 27), vm.Days[0].Date);
        Assert.False(vm.Days[0].IsCurrentMonth);
        Assert.True(vm.Days[4].IsCurrentMonth);
        Assert.Equal("1", vm.Days[4].Number);
    }

    [Fact]
    public void Headers_follow_the_first_day_of_the_week()
    {
        Assert.Equal("DSTQQSS", string.Concat(Create(DayOfWeek.Sunday).WeekDays.Select(w => w.Letter)));
        Assert.Equal("STQQSSD", string.Concat(Create(DayOfWeek.Monday).WeekDays.Select(w => w.Letter)));
        Assert.Equal("Segunda", Create().WeekDays[1].Name);
        Assert.Equal(new DateOnly(2026, 9, 28), Create(DayOfWeek.Monday).Days[0].Date);
    }

    [Fact]
    public void Today_and_selection_are_marked()
    {
        var vm = Create();
        Assert.Single(vm.Days, d => d.IsToday);
        Assert.DoesNotContain(vm.Days, d => d.IsSelected);

        DateOnly? raised = null;
        vm.SelectedChanged += d => raised = d;
        vm.SelectCommand.Execute(vm.Days.First(d => d.Date == new DateOnly(2026, 10, 20)));

        Assert.Equal(new DateOnly(2026, 10, 20), raised);
        Assert.Equal(new DateOnly(2026, 10, 20), vm.Days.Single(d => d.IsSelected).Date);
    }

    [Fact]
    public void Arrows_change_month_and_year()
    {
        var vm = Create();
        vm.NextMonthCommand.Execute(null);
        vm.NextMonthCommand.Execute(null);
        vm.NextMonthCommand.Execute(null);
        Assert.Equal("Janeiro 2027", vm.Title);

        vm.PreviousMonthCommand.Execute(null);
        Assert.Equal("Dezembro 2026", vm.Title);
    }

    [Fact]
    public void Selecting_a_date_in_another_month_jumps_to_it()
    {
        var vm = Create();
        vm.Selected = new DateOnly(2027, 2, 14);
        Assert.Equal("Fevereiro 2027", vm.Title);
        Assert.True(vm.Days.Single(d => d.IsSelected).IsCurrentMonth);
    }

    [Fact]
    public void Suggestions()
    {
        var vm = Create();
        Assert.Equal(new[] { "Hoje", "Amanhã", "Próxima segunda", "+1 semana" }, vm.Suggestions.Select(s => s.Label));
        Assert.Equal(new[] { Today, Today.AddDays(1), new DateOnly(2026, 10, 19), Today.AddDays(7) }, vm.Suggestions.Select(s => s.Date));

        vm.SelectCommand.Execute(vm.Suggestions[2]);
        Assert.Equal(new DateOnly(2026, 10, 19), vm.Selected);
    }

    [Fact]
    public void Accessible_name_is_full()
    {
        var vm = Create();
        Assert.Equal("Quinta, 15 de outubro de 2026", vm.Days.Single(d => d.IsToday).AccessibleName);
    }
}
