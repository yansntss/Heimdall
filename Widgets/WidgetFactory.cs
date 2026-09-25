using Heimdall.Config;

namespace Heimdall.Widgets;

public static class WidgetFactory
{
    /// <summary>Novos widgets (media, reminder...) entram aqui.</summary>
    public static IWidget? Create(string id, AppConfig config, bool isOverlay = false) => id.Trim().ToLowerInvariant() switch
    {
        "clock" => new ClockWidget(config.Clock),
        "media" => new MediaWidget(config, isOverlay),
        "reminder" => new ReminderWidget(config),
        _ => null
    };
}
