namespace Heimdall.Widgets;

/// <summary>Item que um widget acrescenta no topo do próprio menu de contexto (ex.: "Abrir player").</summary>
public sealed record WidgetMenuEntry(string Header, string? IconKey, Action Execute, string? Gesture = null);

/// <summary>
/// Opcional: widget que tem ações próprias no menu de clique direito. O resto do menu
/// (cabeçalho, Mover, Fixar, Configurar, Ocultar) é igual pra todos e vem do WidgetZoneBuilder.
/// </summary>
public interface IWidgetMenuProvider
{
    IEnumerable<WidgetMenuEntry> GetMenuEntries();
}
