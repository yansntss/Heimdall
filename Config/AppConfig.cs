namespace Heimdall.Config;

public enum BarEdge { Top, Bottom, Left, Right }

public enum MonitorMode { Primary, Specific, All }

public sealed class AppConfig
{
    public BarEdge Edge { get; set; } = BarEdge.Top;

    /// <summary>Altura (Top/Bottom) ou largura (Left/Right) em DIPs. Sugestão: 28 horizontal, 72 vertical.</summary>
    public int Thickness { get; set; } = 28;

    public MonitorMode MonitorMode { get; set; } = MonitorMode.Primary;

    /// <summary>Ex.: "DISPLAY2". Usado quando MonitorMode = Specific.</summary>
    public string? MonitorDevice { get; set; }

    public BarStyle Style { get; set; } = new();

    public WidgetLayout Widgets { get; set; } = new();

    public ClockConfig Clock { get; set; } = new();

    public List<ReminderConfig> Reminders { get; set; } = new();

    public bool StartWithWindows { get; set; }

    /// <summary>Nome de um tema embutido ou de %AppData%\Heimdall\themes\*.json.</summary>
    public string Theme { get; set; } = "Escuro";
}

/// <summary>
/// Overrides opcionais por cima do tema selecionado (<see cref="AppConfig.Theme"/>) —
/// nulo/vazio significa "usa o valor do tema".
/// </summary>
public sealed class BarStyle
{
    /// <summary>#AARRGGBB</summary>
    public string? Background { get; set; }
    public string? Foreground { get; set; }
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
}

/// <summary>Três zonas: início (esq./topo), centro e fim (dir./base).</summary>
public sealed class WidgetLayout
{
    public List<string> Start { get; set; } = new();
    public List<string> Center { get; set; } = new() { "clock" };
    public List<string> End { get; set; } = new();
}

public sealed class ClockConfig
{
    public string TimeFormat { get; set; } = "HH:mm";
    public string DateFormat { get; set; } = "ddd, dd/MM/yyyy";
    /// <summary>Formato da data quando a barra está na lateral (espaço menor).</summary>
    public string VerticalDateFormat { get; set; } = "dd/MM";
    public string Culture { get; set; } = "pt-BR";
}
