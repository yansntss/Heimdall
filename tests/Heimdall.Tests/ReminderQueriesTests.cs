using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class ReminderQueriesTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly DateTime Created = new(2026, 10, 1);

    private static ReminderConfig R(string title, int? dayOffset, int? hour = null, bool high = false, bool flagged = false, int createdOffset = 0) => new()
    {
        Title = title,
        DueDate = dayOffset is { } d ? Today.AddDays(d) : null,
        DueTime = hour is { } h ? new TimeOnly(h, 0) : null,
        Priority = high ? ReminderPriority.High : ReminderPriority.None,
        Flagged = flagged,
        CreatedAt = Created.AddMinutes(createdOffset)
    };

    private static string[] Titles(IEnumerable<ReminderConfig> list) => list.Select(r => r.Title).ToArray();

    [Fact]
    public void Today_puts_overdue_first_then_all_day_then_by_time()
    {
        var all = new[]
        {
            R("hoje 18h", 0, 18),
            R("hoje dia todo", 0),
            R("ontem 10h", -1, 10),
            R("hoje 8h", 0, 8),
            R("anteontem dia todo", -2),
        };

        Assert.Equal(new[] { "anteontem dia todo", "ontem 10h", "hoje dia todo", "hoje 8h", "hoje 18h" },
            Titles(ReminderQueries.Today(all, Today)));
    }

    [Fact]
    public void Today_leaves_out_future_undated_and_completed()
    {
        var done = R("concluído", 0, 9);
        done.CompletedAt = DateTime.Now;
        var all = new[] { R("amanhã", 1, 9), R("sem data", null), done, R("hoje", 0, 9) };

        Assert.Equal(new[] { "hoje" }, Titles(ReminderQueries.Today(all, Today)));
    }

    [Fact]
    public void Same_time_ties_break_by_priority_then_flag_then_age()
    {
        var all = new[]
        {
            R("normal novo", 0, 9, createdOffset: 10),
            R("normal antigo", 0, 9, createdOffset: 1),
            R("sinalizado", 0, 9, flagged: true, createdOffset: 20),
            R("alta", 0, 9, high: true, createdOffset: 30),
        };

        Assert.Equal(new[] { "alta", "sinalizado", "normal antigo", "normal novo" }, Titles(ReminderQueries.Today(all, Today)));
    }

    [Fact]
    public void All_puts_undated_after_dated()
    {
        var all = new[] { R("sem data", null), R("semana que vem", 7, 9), R("hoje", 0, 9) };
        Assert.Equal(new[] { "hoje", "semana que vem", "sem data" }, Titles(ReminderQueries.All(all)));
    }

    [Fact]
    public void Flagged_and_list_filters()
    {
        var mercado = R("leite", null);
        mercado.ListId = "mercado";
        var all = new[] { R("sinalizado", 3, flagged: true), mercado, R("comum", 1) };

        Assert.Equal(new[] { "sinalizado" }, Titles(ReminderQueries.Flagged(all)));
        Assert.Equal(new[] { "leite" }, Titles(ReminderQueries.InList(all, "mercado")));
    }
}
