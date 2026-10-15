using System.Windows;

namespace Heimdall.UI;

/// <summary>
/// Tamanhos da barra que dependem da espessura: botões de ícone 30x30 e chips de 28 cabem
/// na barra de 38, mas não na Compacta (26) nem numa barra que o usuário afinou. Sobrescreve
/// os tokens só no escopo da janela da barra — popups e Configurações mantêm o tamanho do design.
/// </summary>
internal static class BarMetrics
{
    public static void Apply(ResourceDictionary resources, int thickness)
    {
        resources["H.Size.IconButton"] = Math.Clamp(thickness - 6.0, 16, 30);
        resources["H.Size.Chip"] = Math.Clamp(thickness - 6.0, 14, 28);
    }
}
