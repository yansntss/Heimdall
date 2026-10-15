using System.Text.Json.Serialization;

namespace Heimdall.Config;

public enum RepeatKind { None, Daily, Weekly, Monthly, Yearly }

public enum ReminderPriority { None, Low, Medium, High }

/// <summary>Regra de repetição. A data da ocorrência pendente fica no próprio lembrete (<see cref="ReminderConfig.DueDate"/>).</summary>
public sealed class ReminderRecurrence
{
    public RepeatKind Kind { get; set; } = RepeatKind.None;

    /// <summary>Weekly: dias da semana. Vazio = o dia da semana de <see cref="Anchor"/>.</summary>
    public List<DayOfWeek> Days { get; set; } = new();

    /// <summary>
    /// Primeira ocorrência — Monthly/Yearly repetem o dia dela, não o da ocorrência
    /// pendente: "todo dia 31" cai em 28/02 e volta pra 31/03, em vez de grudar no 28.
    /// Nulo = usa a data pendente.
    /// </summary>
    public DateOnly? Anchor { get; set; }

    /// <summary>"A cada N" dias/semanas/meses/anos (contados a partir de <see cref="Anchor"/>). 1 = todo.</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Último dia em que ainda pode ocorrer. Nulo = sem fim.</summary>
    public DateOnly? Until { get; set; }

    /// <summary>"Após N vezes": total de ocorrências da série, contando a da âncora. Nulo = sem limite.</summary>
    public int? Count { get; set; }
}

/// <summary>Lista do usuário ("Mercado", "Trabalho"...) — todo lembrete pertence a uma.</summary>
public sealed class ReminderListConfig
{
    public const string DefaultId = "default";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    /// <summary>"#RRGGBB".</summary>
    public string Color { get; set; } = "#9DB8F2";
}

/// <summary>Preferências gerais dos lembretes (seção Lembretes das Configurações).</summary>
public sealed class ReminderOptions
{
    /// <summary>Lembrete com data e sem hora notifica neste horário.</summary>
    public TimeOnly DefaultTime { get; set; } = new(9, 0);

    public int SnoozeMinutes { get; set; } = 10;

    public bool PlaySound { get; set; }

    public bool ShowCompleted { get; set; }
}

public sealed class ReminderConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "";

    public string? Note { get; set; }

    /// <summary>Data da ocorrência pendente. Nulo = lembrete sem data (fica fixo na barra até ser concluído).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Nulo com <see cref="DueDate"/> preenchido = "dia todo": notifica na hora padrão.</summary>
    public TimeOnly? DueTime { get; set; }

    /// <summary>Nulo = não repete.</summary>
    public ReminderRecurrence? Recurrence { get; set; }

    public bool Flagged { get; set; }

    public ReminderPriority Priority { get; set; } = ReminderPriority.None;

    public string ListId { get; set; } = ReminderListConfig.DefaultId;

    /// <summary>Concluído de vez. Recorrentes só ganham isso quando a repetição acaba — concluir uma ocorrência avança a data.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Adiar": notifica de novo neste instante em vez da data/hora normal.</summary>
    public DateTimeOffset? SnoozedUntil { get; set; }

    /// <summary>Último disparo — evita notificar duas vezes a mesma ocorrência (inclusive entre execuções).</summary>
    public DateTimeOffset? NotifiedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    [JsonIgnore]
    public bool IsCompleted => CompletedAt is not null;

    [JsonIgnore]
    public bool Repeats => Recurrence is { Kind: not RepeatKind.None };
}
