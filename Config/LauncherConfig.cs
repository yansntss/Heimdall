namespace Heimdall.Config;

/// <summary>Um atalho no widget de launcher — executável, .lnk, pasta, arquivo, URL ou app da Store.</summary>
public sealed class LauncherConfig
{
    public string Name { get; set; } = "";

    /// <summary>Caminho de arquivo/pasta, URL, ou "shell:AppsFolder\{AUMID}" pra apps da Store.</summary>
    public string Path { get; set; } = "";

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>Caminho de um ícone customizado — nulo usa o ícone extraído do próprio Path.</summary>
    public string? IconPath { get; set; }

    public bool RunAsAdmin { get; set; }
}
