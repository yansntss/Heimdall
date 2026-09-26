using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Widgets;

namespace Heimdall.UI;

/// <summary>Monta as 3 zonas (início/centro/fim) de widgets — usado pela BarWindow e pela OverlayWindow.</summary>
internal static class WidgetZoneBuilder
{
    private const int HoverTransitionMs = 150;

    // 8 direções ao redor, 1px cada — dá um contorno fechado sem depender de blur (DropShadowEffect).
    private static readonly (double dx, double dy)[] OutlineOffsets =
    {
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1)
    };

    public static List<IWidget> Build(AppConfig cfg, bool vertical, Grid zones, StackPanel start, StackPanel center, StackPanel end, bool isOverlay = false)
    {
        var widgets = new List<IWidget>();
        var orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        var style = ThemeService.GetEffectiveStyle(cfg);
        var separatorBrush = new SolidColorBrush(style.Border);
        separatorBrush.Freeze();

        // Contorno sempre no extremo oposto da luminância do texto: garante contraste
        // do contorno contra o próprio texto (e por consequência contra qualquer fundo
        // do jogo por trás), sem precisar saber a cor do que está atrás no overlay.
        double luminance = 0.299 * style.Foreground.R + 0.587 * style.Foreground.G + 0.114 * style.Foreground.B;
        var outlineColor = luminance > 128 ? Colors.Black : Colors.White;

        zones.Margin = vertical ? new Thickness(0, 8, 0, 8) : new Thickness(8, 0, 8, 0);

        SetupZone(widgets, cfg, start, cfg.Widgets.Start, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            vertical ? VerticalAlignment.Top : VerticalAlignment.Center, isOverlay, separatorBrush, style.Hover, outlineColor);

        SetupZone(widgets, cfg, center, cfg.Widgets.Center, orientation,
            HorizontalAlignment.Center, VerticalAlignment.Center, isOverlay, separatorBrush, style.Hover, outlineColor);

        SetupZone(widgets, cfg, end, cfg.Widgets.End, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center, isOverlay, separatorBrush, style.Hover, outlineColor);

        // Separador entre zona e centro, do lado que fica voltado pro centro: último
        // filho de Start (mais próximo do centro, já que Start é alinhado à esquerda/topo)
        // e primeiro filho de End (idem, alinhado à direita/base).
        if (start.Children.Count > 0) start.Children.Add(CreateSeparator(orientation, separatorBrush));
        if (end.Children.Count > 0) end.Children.Insert(0, CreateSeparator(orientation, separatorBrush));

        return widgets;
    }

    private static void SetupZone(List<IWidget> widgets, AppConfig cfg, StackPanel zone, IEnumerable<string>? ids,
        Orientation orientation, HorizontalAlignment horizontal, VerticalAlignment vertical, bool isOverlay, Brush separatorBrush, Color hoverColor, Color outlineColor)
    {
        zone.Orientation = orientation;
        zone.HorizontalAlignment = horizontal;
        zone.VerticalAlignment = vertical;

        if (ids is null) return;

        foreach (var id in ids)
        {
            var widget = WidgetFactory.Create(id, cfg, isOverlay);
            if (widget is null) continue;

            widget.ApplyOrientation(orientation);

            if (zone.Children.Count > 0) zone.Children.Add(CreateSeparator(orientation, separatorBrush));

            // Overlay é só informativo e clique atravessa — sem destaque de hover ali,
            // mas com contorno no texto pra ler sobre qualquer fundo de jogo.
            zone.Children.Add(isOverlay
                ? WrapWithMargin(WrapWithOutline(widget.View, outlineColor), orientation)
                : WrapWithHover(widget.View, orientation, hoverColor));
            widgets.Add(widget);
            widget.Start();
        }
    }

    private static FrameworkElement WrapWithMargin(FrameworkElement view, Orientation orientation)
    {
        view.Margin = orientation == Orientation.Vertical
            ? new Thickness(0, 4, 0, 4)
            : new Thickness(8, 0, 8, 0);
        return view;
    }

    /// <summary>
    /// Contorno via texto/ícone duplicado 8x deslocado 1px (não DropShadowEffect, que borra
    /// em vez de dar um traço definido): cada cópia usa a máscara de opacidade do próprio
    /// conteúdo (silhueta), preenchida com a cor de contorno, atrás do conteúdo real —
    /// funciona pra qualquer visual (texto, ícones), não só TextBlock.
    /// </summary>
    private static FrameworkElement WrapWithOutline(FrameworkElement view, Color outlineColor)
    {
        var outlineBrush = new SolidColorBrush(outlineColor);
        outlineBrush.Freeze();
        var mask = new VisualBrush(view) { Stretch = Stretch.None };

        var grid = new Grid();
        foreach (var (dx, dy) in OutlineOffsets)
        {
            grid.Children.Add(new Rectangle
            {
                Fill = outlineBrush,
                OpacityMask = mask,
                IsHitTestVisible = false,
                RenderTransform = new TranslateTransform(dx, dy)
            });
        }
        grid.Children.Add(view);
        return grid;
    }

    /// <summary>Envolve o widget num Border que acende sutilmente (cor "Hover" do tema) ao passar o mouse, com transição de 150 ms.</summary>
    private static Border WrapWithHover(FrameworkElement view, Orientation orientation, Color hoverColor)
    {
        var background = new SolidColorBrush(Colors.Transparent);
        var wrapper = new Border
        {
            Child = view,
            CornerRadius = new CornerRadius(4),
            Background = background,
            Margin = orientation == Orientation.Vertical
                ? new Thickness(0, 4, 0, 4)
                : new Thickness(8, 0, 8, 0),
            Padding = new Thickness(4, 2, 4, 2)
        };
        view.Margin = new Thickness(0);

        wrapper.MouseEnter += (_, _) => AnimateHover(background, hoverColor);
        wrapper.MouseLeave += (_, _) => AnimateHover(background, Colors.Transparent);

        return wrapper;
    }

    private static void AnimateHover(SolidColorBrush brush, Color target)
    {
        var animation = new ColorAnimation(target, TimeSpan.FromMilliseconds(HoverTransitionMs));
        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    /// <summary>Linha fina (1px) usando a cor de contorno do tema — some sozinha em overlays sem tema aplicado.</summary>
    private static Border CreateSeparator(Orientation orientation, Brush brush) => orientation == Orientation.Vertical
        ? new Border { Background = brush, Height = 1, Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Stretch }
        : new Border { Background = brush, Width = 1, Margin = new Thickness(0, 4, 0, 4), VerticalAlignment = VerticalAlignment.Stretch };
}
