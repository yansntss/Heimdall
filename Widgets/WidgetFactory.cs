using Heimdall.Config;

namespace Heimdall.Widgets;

public static class WidgetFactory
{
    /// <summary>Catálogo de widgets disponíveis (id + ícone) — usado pela tela de Configurações e pelo menu "Adicionar widget" da barra.</summary>
    public static readonly (string Id, string Icon)[] Catalog =
    {
        ("clock", "🕐"), ("media", "🎵"), ("reminder", "⏰"), ("launcher", "🚀"),
        ("windows", "🗔"), ("volume", "🔊"), ("mic", "🎤"), ("ram", "🧠"), ("temp", "🌡️"), ("fps", "🎮"), ("separator", "┃")
    };

    /// <summary>Novos widgets (media, reminder...) entram aqui.</summary>
    public static IWidget? Create(string id, AppConfig config, bool isOverlay = false) => id.Trim().ToLowerInvariant() switch
    {
        "clock" => new ClockWidget(config, isOverlay),
        "media" => new MediaWidget(config, isOverlay),
        "reminder" => new ReminderWidget(config),
        // Oculto no overlay: lançar um app clicando em cima do jogo não faz sentido ali.
        "launcher" => isOverlay ? null : new LauncherWidget(config),
        // Oculto no overlay: o clique atravessa a janela ali, então a lista não seria utilizável.
        "windows" => isOverlay ? null : new WindowsWidget(config),
        "volume" => new VolumeWidget(config, isMic: false, isOverlay),
        "mic" => new VolumeWidget(config, isMic: true, isOverlay),
        "separator" => new SeparatorWidget(config),
        "ram" => new RamWidget(config.Ram),
        "temp" => new TempWidget(config.Temp),
        // Sem sentido fora de jogo por padrão (Fps.OnlyInGame) — nem cria o widget na
        // barra normal nesse caso, só no overlay.
        "fps" => config.Fps.OnlyInGame && !isOverlay ? null : new FpsWidget(),
        _ => null
    };
}
