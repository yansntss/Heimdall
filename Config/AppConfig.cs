namespace Heimdall.Config;

public enum AppLanguage { PtBr, EnUs }

public enum BarEdge { Top, Bottom, Left, Right }

public enum MonitorMode { Primary, Specific, All }

public sealed class AppConfig
{
    public BarEdge Edge { get; set; } = BarEdge.Top;

    /// <summary>Altura (Top/Bottom) ou largura (Left/Right) em DIPs. Sugestão: 28 horizontal, 72 vertical.</summary>
    public int Thickness { get; set; } = 28;

    /// <summary>Modo flutuante: barra com margem das bordas da tela e cantos arredondados.</summary>
    public bool FloatingMode { get; set; }

    /// <summary>Margem em DIPs ao redor da barra quando <see cref="FloatingMode"/> está ativo.</summary>
    public int FloatingMargin { get; set; } = 8;

    public MonitorMode MonitorMode { get; set; } = MonitorMode.Primary;

    /// <summary>Ex.: "DISPLAY2". Usado quando MonitorMode = Specific.</summary>
    public string? MonitorDevice { get; set; }

    public BarStyle Style { get; set; } = new();

    public WidgetLayout Widgets { get; set; } = new();

    public ClockConfig Clock { get; set; } = new();

    public List<ReminderConfig> Reminders { get; set; } = new();

    public List<LauncherConfig> Launchers { get; set; } = new();

    public RamConfig Ram { get; set; } = new();

    public TempConfig Temp { get; set; } = new();

    public FpsConfig Fps { get; set; } = new();

    public WindowsWidgetConfig Windows { get; set; } = new();

    public bool StartWithWindows { get; set; }

    /// <summary>Nome de um tema embutido ou de %AppData%\Heimdall\themes\*.json.</summary>
    public string Theme { get; set; } = "Escuro";

    /// <summary>
    /// true (padrão) = ao detectar tela cheia, a barra vira overlay transparente com
    /// clique atravessando. false = a barra simplesmente some (Hide) e volta ao sair,
    /// sem overlay nenhum.
    /// </summary>
    public bool GamingMode { get; set; } = true;

    public AppLanguage Language { get; set; } = AppLanguage.PtBr;

    /// <summary>Consulta a última release no GitHub ao abrir (e a cada algumas horas) e avisa quando tem versão nova.</summary>
    public bool CheckForUpdates { get; set; } = true;
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

/// <summary>Uma entrada de widget numa zona: qual widget e se está fixado (não se move nem é movido por arraste).</summary>
public sealed class WidgetEntry
{
    public string Id { get; set; } = "";
    public bool Pinned { get; set; }

    public WidgetEntry() { }

    public WidgetEntry(string id, bool pinned = false)
    {
        Id = id;
        Pinned = pinned;
    }
}

/// <summary>Três zonas: início (esq./topo), centro e fim (dir./base).</summary>
public sealed class WidgetLayout
{
    public List<WidgetEntry> Start { get; set; } = new();
    public List<WidgetEntry> Center { get; set; } = new() { new WidgetEntry("clock") };
    public List<WidgetEntry> End { get; set; } = new();
}

public enum ClockMode { TimeOnly, DateOnly, Both, Custom }

public enum ClockStyle { Classic, Compact, Verbose, ISO }

public sealed class ClockConfig
{
    public ClockMode Mode { get; set; } = ClockMode.Both;
    public ClockStyle Style { get; set; } = ClockStyle.Classic;
    public string Culture { get; set; } = "pt-BR";

    /// <summary>Usado só quando Mode = Custom — formato .NET livre, sem variação por orientação.</summary>
    public string? CustomFormat { get; set; }
}

public sealed class RamConfig
{
    /// <summary>true = "11.2 / 32 GB"; false = só a porcentagem ("35%").</summary>
    public bool ShowUsedTotal { get; set; } = true;
}

public sealed class TempConfig
{
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
}

public sealed class FpsConfig
{
    /// <summary>true (padrão) = só aparece no modo overlay (tela cheia); false = aparece sempre.</summary>
    public bool OnlyInGame { get; set; } = true;
}

public sealed class WindowsWidgetConfig
{
    /// <summary>true (padrão) = janelas do mesmo executável agrupadas sob um ícone; false = um item por janela.</summary>
    public bool GroupWindows { get; set; } = true;

    /// <summary>Mostra o título ao lado do ícone — útil em barra larga, ruim em barra estreita.</summary>
    public bool ShowTitles { get; set; }

    /// <summary>Acima disso, o excedente vira um item "+N" que abre a lista completa num popup.</summary>
    public int MaxItems { get; set; } = 15;

    /// <summary>Nomes de executável (ex.: "backgroundtaskhost.exe") a nunca exibir, mesmo passando pelos filtros de janela.</summary>
    public List<string> ExcludeProcesses { get; set; } = new();
}
