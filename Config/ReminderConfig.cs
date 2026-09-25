namespace Heimdall.Config;

public enum ReminderKind { Fixed, Scheduled }

public enum ReminderRecurrence { Once, Daily, Weekly }

public sealed class ReminderConfig
{
    public ReminderKind Kind { get; set; } = ReminderKind.Fixed;

    public string Text { get; set; } = "";

    /// <summary>"HH:mm". Usado apenas quando Kind = Scheduled.</summary>
    public string? Time { get; set; }

    public ReminderRecurrence Recurrence { get; set; } = ReminderRecurrence.Once;

    /// <summary>Usado apenas quando Recurrence = Weekly.</summary>
    public List<DayOfWeek> Days { get; set; } = new();

    public bool PlaySound { get; set; }

    /// <summary>Marcado automaticamente após um lembrete "Once" disparar; persistido no config.json.</summary>
    public bool Completed { get; set; }
}
