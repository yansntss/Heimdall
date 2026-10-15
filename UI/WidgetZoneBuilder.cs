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

    /// <summary>Widgets que sobram na variante Compacta (marca, mídia, badge e hora).</summary>
    private static readonly HashSet<string> CompactWidgets = new(StringComparer.OrdinalIgnoreCase) { "brand", "media", "notifications", "clock" };

    // 8 direções ao redor, 1px cada — dá um contorno fechado sem depender de blur (DropShadowEffect).
    private static readonly (double dx, double dy)[] OutlineOffsets =
    {
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1)
    };

    public static List<IWidget> Build(AppConfig cfg, bool vertical, Grid zones, StackPanel start, StackPanel center, StackPanel end, bool isOverlay = false, bool narrowBar = false)
    {
        var widgets = new List<IWidget>();
        var orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        var style = ThemeService.GetEffectiveStyle(cfg);

        // Contorno sempre no extremo oposto da luminância do texto: garante contraste
        // do contorno contra o próprio texto (e por consequência contra qualquer fundo
        // do jogo por trás), sem precisar saber a cor do que está atrás no overlay.
        double luminance = 0.299 * style.Foreground.R + 0.587 * style.Foreground.G + 0.114 * style.Foreground.B;
        var outlineColor = luminance > 128 ? Colors.Black : Colors.White;

        LayoutZones(zones, start, center, end, vertical);

        // Arrastar/fixar só fazem sentido na barra de verdade — no overlay é só informativo
        // e o clique atravessa, então nem entra na jogada.
        WidgetDragController? drag = isOverlay ? null : new WidgetDragController(cfg, zones, vertical);
        drag?.RegisterZone(start, cfg.Widgets.Start);
        drag?.RegisterZone(center, cfg.Widgets.Center);
        drag?.RegisterZone(end, cfg.Widgets.End);

        SetupZone(widgets, cfg, start, cfg.Widgets.Start, 0, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            vertical ? VerticalAlignment.Top : VerticalAlignment.Center, isOverlay, narrowBar, outlineColor, drag);

        SetupZone(widgets, cfg, center, cfg.Widgets.Center, 1, orientation,
            HorizontalAlignment.Center, VerticalAlignment.Center, isOverlay, narrowBar, outlineColor, drag);

        SetupZone(widgets, cfg, end, cfg.Widgets.End, 2, orientation,
            vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center, isOverlay, narrowBar, outlineColor, drag);

        return widgets;
    }

    /// <summary>
    /// Grid de 3 trilhas — Auto / * / Auto (colunas na horizontal, linhas na vertical): início
    /// e fim ocupam o que precisam e o centro fica centralizado no espaço que sobra.
    /// </summary>
    private static void LayoutZones(Grid zones, StackPanel start, StackPanel center, StackPanel end, bool vertical)
    {
        zones.ColumnDefinitions.Clear();
        zones.RowDefinitions.Clear();
        zones.Margin = new Thickness(0);

        var panels = new[] { start, center, end };
        for (int i = 0; i < 3; i++)
        {
            var size = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            if (vertical) zones.RowDefinitions.Add(new RowDefinition { Height = size });
            else zones.ColumnDefinitions.Add(new ColumnDefinition { Width = size });

            Grid.SetColumn(panels[i], vertical ? 0 : i);
            Grid.SetRow(panels[i], vertical ? i : 0);
        }
    }

    private static void SetupZone(List<IWidget> widgets, AppConfig cfg, StackPanel zone, List<WidgetEntry> entries, int zoneIndex,
        Orientation orientation, HorizontalAlignment horizontal, VerticalAlignment vertical, bool isOverlay, bool narrowBar,
        Color outlineColor, WidgetDragController? drag)
    {
        zone.Orientation = orientation;
        zone.HorizontalAlignment = horizontal;
        zone.VerticalAlignment = vertical;

        bool compact = ThemeManager.Palette.IsCompact && orientation == Orientation.Horizontal;

        foreach (var entry in entries)
        {
            if (compact && !CompactWidgets.Contains(entry.Id)) continue;

            var widget = WidgetFactory.Create(entry.Id, cfg, isOverlay, narrowBar);
            if (widget is null) continue;

            widget.ApplyOrientation(orientation);

            if (isOverlay)
            {
                // Overlay é só informativo e clique atravessa — sem destaque de hover, sem
                // arrastar/fixar, só o contorno no texto pra ler sobre qualquer fundo de jogo.
                zone.Children.Add(WrapWithMargin(WrapWithOutline(widget.View, outlineColor), orientation));
            }
            else
            {
                var wrapper = WrapSlot(widget.View, orientation, entry.Pinned);
                wrapper.ContextMenu = BuildWidgetContextMenu(cfg, entry);
                drag?.RegisterSlot(wrapper, entry, zoneIndex);
                zone.Children.Add(wrapper);
            }

            widgets.Add(widget);
            widget.Start();
        }
    }

    /// <summary>Fixar/desafixar ou remover um widget inteiro (clique direito em qualquer parte dele que não tenha o próprio menu, como o ícone de um atalho).</summary>
    private static ContextMenu BuildWidgetContextMenu(AppConfig cfg, WidgetEntry entry)
    {
        var toggle = new MenuItem { Header = entry.Pinned ? Strings.Unpin : Strings.Pin };
        toggle.Click += (_, _) =>
        {
            entry.Pinned = !entry.Pinned;
            ConfigService.Save(cfg);
            ((App)Application.Current).Reload();
        };

        var remove = new MenuItem { Header = Strings.RemoveWidget };
        remove.Click += (_, _) =>
        {
            // Mesma referência que está numa das 3 zonas do cfg — remove de onde estiver.
            if (!cfg.Widgets.Start.Remove(entry) && !cfg.Widgets.Center.Remove(entry))
                cfg.Widgets.End.Remove(entry);
            ConfigService.Save(cfg);
            ((App)Application.Current).Reload();
        };

        return new ContextMenu { Items = { toggle, new Separator(), remove } };
    }

    private static FrameworkElement WrapWithMargin(FrameworkElement view, Orientation orientation)
    {
        view.Margin = orientation == Orientation.Vertical
            ? new Thickness(0, 2, 0, 2)
            : new Thickness(2, 0, 2, 0);
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

    /// <summary>
    /// Envolve o widget no "slot" que o arraste e o menu de contexto usam. Sem destaque
    /// próprio: cada widget já tem o hover dele (botões 30x30, chips). Quando fixado,
    /// mostra um alfinete discreto no canto que só aparece no hover (fade de 150 ms).
    /// Fundo transparente (não nulo) pra área entre os elementos do widget também pegar o arraste.
    /// </summary>
    private static Border WrapSlot(FrameworkElement view, Orientation orientation, bool pinned)
    {
        FrameworkElement content = view;
        TextBlock? pinIcon = null;

        if (pinned)
        {
            pinIcon = new TextBlock
            {
                Text = "📌",
                FontSize = 9,
                Opacity = 0,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -3, -3, 0)
            };
            var grid = new Grid();
            grid.Children.Add(view);
            grid.Children.Add(pinIcon);
            content = grid;
        }

        var wrapper = new Border
        {
            Child = content,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            // Espaçamento base 4px entre widgets (2 de cada lado).
            Margin = orientation == Orientation.Vertical
                ? new Thickness(0, 2, 0, 2)
                : new Thickness(2, 0, 2, 0)
        };
        view.Margin = new Thickness(0);

        if (pinIcon is not null)
        {
            wrapper.MouseEnter += (_, _) => pinIcon.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(HoverTransitionMs)));
            wrapper.MouseLeave += (_, _) => pinIcon.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(HoverTransitionMs)));
        }

        return wrapper;
    }
}
