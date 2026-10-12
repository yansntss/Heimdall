namespace Heimdall.Config;

/// <summary>
/// Group: cabeçalho de grupo nomeado (ex.: "Apps", "Jogos") — desenhado como separador, e
/// todos os itens depois dele (até o próximo Group) pertencem a ele. Itens antes do
/// primeiro Group ficam "sem grupo".
/// </summary>
public enum LauncherItemType { App, Separator, Group }

public enum SeparatorStyle { Line, Space, Dot }

/// <summary>
/// Como um grupo aparece na barra: Auto = vira pasta só em barra apertada (horizontal num
/// monitor em pé); Always/Never forçam pasta ou atalhos soltos.
/// </summary>
public enum GroupFolderMode { Auto, Always, Never }

/// <summary>
/// Um item da lista de atalhos: um app (executável, .lnk, pasta, arquivo, URL ou app da
/// Store) ou um separador visual entre eles. <see cref="Type"/> decide quais campos valem.
/// </summary>
public sealed class LauncherConfig
{
    public LauncherItemType Type { get; set; } = LauncherItemType.App;

    public string Name { get; set; } = "";

    /// <summary>Caminho de arquivo/pasta, URL, ou "shell:AppsFolder\{AUMID}" pra apps da Store.</summary>
    public string Path { get; set; } = "";

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>Caminho de um ícone customizado — nulo usa o ícone extraído do próprio Path.</summary>
    public string? IconPath { get; set; }

    public bool RunAsAdmin { get; set; }

    /// <summary>Usado só quando Type = Separator ou Group.</summary>
    public SeparatorStyle Style { get; set; } = SeparatorStyle.Line;

    /// <summary>Usado só quando Type = Group: mostrar o grupo como uma pasta (um ícone que abre a lista dos atalhos).</summary>
    public GroupFolderMode Folder { get; set; } = GroupFolderMode.Auto;
}
