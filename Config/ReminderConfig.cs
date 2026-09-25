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

    /// <summary>Quando foi criado — usado só pro histórico ("criado dd/MM HH:mm"). Nulo em lembretes de configs antigas.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Dia específico pra lembretes "Once" (ex: "Amanhã 9h" do popup de adição rápida).
    /// Nulo dispara no primeiro HH:mm que bater, independente do dia — mantém o
    /// comportamento de sempre pros lembretes existentes que não têm essa data.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>Usado só quando Recurrence = Daily/Weekly. Nulo = sem limite nesse lado.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Usado só quando Recurrence = Daily/Weekly. Nulo = sem limite nesse lado.</summary>
    public DateOnly? EndDate { get; set; }
}
