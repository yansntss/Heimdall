using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Heimdall.Config;

namespace Heimdall.Tests;

public class ReminderMigrationTests
{
    // Quinta, 15/10/2026, 10:00.
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static ReminderConfig Migrate(string legacyReminderJson)
    {
        var root = JsonNode.Parse($"{{\"Reminders\": [{legacyReminderJson}]}}")!.AsObject();
        ReminderMigration.Migrate(root, Now);
        return root["Reminders"]![0].Deserialize<ReminderConfig>(Options)!;
    }

    [Fact]
    public void Fixed_becomes_undated()
    {
        var r = Migrate("""{"Kind": "Fixed", "Text": "Beber água"}""");
        Assert.Equal("Beber água", r.Title);
        Assert.Null(r.DueDate);
        Assert.False(r.IsCompleted);
        Assert.False(string.IsNullOrEmpty(r.Id));
    }

    [Fact]
    public void Once_with_date_keeps_date_and_time()
    {
        var r = Migrate("""{"Kind": "Scheduled", "Text": "Dentista", "Time": "14:30", "Recurrence": "Once", "Date": "2026-10-20"}""");
        Assert.Equal(new DateOnly(2026, 10, 20), r.DueDate);
        Assert.Equal(new TimeOnly(14, 30), r.DueTime);
        Assert.Null(r.Recurrence);
    }

    [Fact]
    public void Once_without_date_goes_to_the_next_time_it_would_fire()
    {
        Assert.Equal(new DateOnly(2026, 10, 15), Migrate("""{"Kind": "Scheduled", "Text": "a", "Time": "18:00"}""").DueDate);
        Assert.Equal(new DateOnly(2026, 10, 16), Migrate("""{"Kind": "Scheduled", "Text": "b", "Time": "08:00"}""").DueDate);
    }

    [Fact]
    public void Completed_once_stays_completed()
    {
        var r = Migrate("""{"Kind": "Scheduled", "Text": "a", "Time": "08:00", "Date": "2026-10-10", "Completed": true}""");
        Assert.True(r.IsCompleted);
        Assert.Equal(new DateOnly(2026, 10, 10), r.DueDate);

        var undated = Migrate("""{"Kind": "Scheduled", "Text": "b", "Time": "08:00", "Completed": true}""");
        Assert.True(undated.IsCompleted);
        Assert.Equal(new DateOnly(2026, 10, 15), undated.DueDate);
    }

    [Fact]
    public void Daily_already_past_today_goes_to_tomorrow()
    {
        var r = Migrate("""{"Kind": "Scheduled", "Text": "Remédio", "Time": "08:00", "Recurrence": "Daily"}""");
        Assert.Equal(RepeatKind.Daily, r.Recurrence!.Kind);
        Assert.Equal(new DateOnly(2026, 10, 16), r.DueDate);
    }

    [Fact]
    public void Daily_done_early_today_goes_to_tomorrow()
    {
        var r = Migrate("""{"Kind": "Scheduled", "Text": "Remédio", "Time": "20:00", "Recurrence": "Daily", "DoneOn": "2026-10-15"}""");
        Assert.Equal(new DateOnly(2026, 10, 16), r.DueDate);
    }

    [Fact]
    public void Weekly_keeps_days_start_and_end()
    {
        var r = Migrate("""
            {"Kind": "Scheduled", "Text": "Academia", "Time": "19:00", "Recurrence": "Weekly",
             "Days": ["Monday", "Wednesday"], "StartDate": "2026-10-20", "EndDate": "2026-12-31"}
            """);
        Assert.Equal(new DateOnly(2026, 10, 21), r.DueDate);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, r.Recurrence!.Days);
        Assert.Equal(new DateOnly(2026, 12, 31), r.Recurrence.Until);
    }

    [Fact]
    public void Recurring_past_its_end_is_completed()
    {
        var r = Migrate("""{"Kind": "Scheduled", "Text": "a", "Time": "19:00", "Recurrence": "Daily", "EndDate": "2026-10-01"}""");
        Assert.True(r.IsCompleted);
    }

    [Fact]
    public void Weekly_without_days_never_fired_and_becomes_undated()
    {
        var r = Migrate("""{"Kind": "Scheduled", "Text": "a", "Time": "19:00", "Recurrence": "Weekly", "Days": []}""");
        Assert.Null(r.DueDate);
    }

    [Fact]
    public void Garbage_does_not_throw()
    {
        var r = Migrate("""{"Kind": 3, "Text": null, "Time": "25:99", "Recurrence": {"x": 1}, "Days": "Monday"}""");
        Assert.Equal("", r.Title);
        Assert.Null(r.DueDate);
    }
}
