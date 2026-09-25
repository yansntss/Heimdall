using System.Windows;
using System.Windows.Controls;
using Heimdall.Config;
using Heimdall.Widgets;

namespace Heimdall.UI;

/// <summary>Monta as 3 zonas (início/centro/fim) de widgets — usado pela BarWindow e pela OverlayWindow.</summary>
internal static class WidgetZoneBuilder
{
    public static List<IWidget> Build(AppConfig cfg, bool vertical, Grid zones, StackPanel start, StackPanel center, StackPanel end)
    {
        var widgets = new List<IWidget>();
        var orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;

        zones.Margin = vertical ? new Thickness(0, 8, 0, 8) : new Thickness(8, 0, 8, 0);

        SetupZone(widgets, cfg, start, cfg.Widgets.Start, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            vertical ? VerticalAlignment.Top : VerticalAlignment.Center);

        SetupZone(widgets, cfg, center, cfg.Widgets.Center, orientation,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        SetupZone(widgets, cfg, end, cfg.Widgets.End, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center);

        return widgets;
    }

    private static void SetupZone(List<IWidget> widgets, AppConfig cfg, StackPanel zone, IEnumerable<string>? ids,
        Orientation orientation, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        zone.Orientation = orientation;
        zone.HorizontalAlignment = horizontal;
        zone.VerticalAlignment = vertical;

        if (ids is null) return;

        foreach (var id in ids)
        {
            var widget = WidgetFactory.Create(id, cfg);
            if (widget is null) continue;

            widget.ApplyOrientation(orientation);
            widget.View.Margin = orientation == Orientation.Vertical
                ? new Thickness(0, 4, 0, 4)
                : new Thickness(8, 0, 8, 0);

            zone.Children.Add(widget.View);
            widgets.Add(widget);
            widget.Start();
        }
    }
}
