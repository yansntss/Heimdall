using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Native;

namespace Heimdall.UI;

/// <summary>
/// Arraste manual pra reordenar widgets inteiros entre as 3 zonas da barra — mesma
/// mecânica do <see cref="Widgets.LauncherWidget"/> (fantasma seguindo o cursor, vizinhos
/// deslizando, soltar com quique), generalizada: em vez de recriar o visual do item (como
/// dá pra fazer com um ícone), o fantasma é um snapshot renderizado do próprio widget, já
/// que cada widget tem uma aparência completamente diferente. Zonas vazias continuam
/// sendo um alvo válido: a barra é dividida em 3 terços (início/centro/fim) pra saber
/// pra qual zona o cursor aponta, já que as 3 StackPanels ficam sobrepostas na mesma
/// célula e uma zona vazia não tem área própria pra receber o cursor.
/// </summary>
internal sealed class WidgetDragController
{
    private sealed class ZoneInfo
    {
        public required StackPanel Panel;
        public required List<WidgetEntry> Entries;
    }

    private sealed class SlotInfo
    {
        public required FrameworkElement Wrapper;
        public required WidgetEntry Entry;
        public required int ZoneIndex;
    }

    private sealed class SiblingInfo
    {
        public required FrameworkElement Element;
        public required double OriginalOffset;
        public required double Size;
        public required TranslateTransform Transform;
    }

    private sealed class DragState
    {
        public required SlotInfo Slot;
        public required GhostIconWindow Ghost;
        public required int OriginZone;
        public required int OriginIndex;
        public required Point OriginalScreenCenter;
        public required bool Animate;
        public required double SlotSize;
        public int CurrentZone;
        public int CurrentTarget;
        public List<SiblingInfo> Others = new();
    }

    private readonly AppConfig _cfg;
    private readonly FrameworkElement _zonesRoot;
    private readonly bool _vertical;
    private readonly Func<FrameworkElement> _createSeparator;
    private readonly List<ZoneInfo> _zones = new();
    private readonly HashSet<FrameworkElement> _wrappers = new();
    private readonly Dictionary<WidgetEntry, FrameworkElement> _wrapperByEntry = new();

    // Esc só é lido de dentro do PreviewMouseMove/timer, igual ao arraste do launcher —
    // a barra é WS_EX_NOACTIVATE e não recebe foco de teclado de verdade.
    private readonly DispatcherTimer _escapeWatchTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };

    private Point? _pressPoint;
    private SlotInfo? _pressSlot;
    private DragState? _drag;

    /// <summary>
    /// <paramref name="createSeparator"/> é o mesmo factory de separador do
    /// WidgetZoneBuilder — usado só pra reconstruir os separadores de uma zona depois de
    /// mover um widget nela, sem precisar de <c>App.Reload()</c> (que destruiria e
    /// recriaria a janela inteira, reiniciando timers e reconectando o SMTC à toa).
    /// </summary>
    public WidgetDragController(AppConfig cfg, FrameworkElement zonesRoot, bool vertical, Func<FrameworkElement> createSeparator)
    {
        _cfg = cfg;
        _zonesRoot = zonesRoot;
        _vertical = vertical;
        _createSeparator = createSeparator;
        _escapeWatchTimer.Tick += (_, _) =>
        {
            if (_drag is not null && (NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0)
                EndDrag(commit: false);
        };
    }

    public void RegisterZone(StackPanel panel, List<WidgetEntry> entries) =>
        _zones.Add(new ZoneInfo { Panel = panel, Entries = entries });

    /// <summary>Liga o arraste a um widget já colocado num wrapper — chamado pelo WidgetZoneBuilder na ordem em que aparecem na zona.</summary>
    public void RegisterSlot(FrameworkElement wrapper, WidgetEntry entry, int zoneIndex)
    {
        var slot = new SlotInfo { Wrapper = wrapper, Entry = entry, ZoneIndex = zoneIndex };
        _wrappers.Add(wrapper);
        _wrapperByEntry[entry] = wrapper;

        wrapper.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (entry.Pinned)
            {
                // "Cursor bloqueado" — widget fixado não pode ser arrastado.
                wrapper.Cursor = Cursors.No;
                return;
            }
            _pressPoint = e.GetPosition(null);
            _pressSlot = slot;
            wrapper.CaptureMouse();
        };

        wrapper.PreviewMouseMove += (_, e) =>
        {
            if (_drag is not null && _drag.Slot == slot)
            {
                UpdateDrag(e);
                return;
            }

            if (_pressPoint is null || _pressSlot != slot || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _pressPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _pressPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            _pressPoint = null;
            _pressSlot = null;
            BeginDrag(slot);
        };

        wrapper.PreviewMouseLeftButtonUp += (_, _) =>
        {
            _pressPoint = null;
            _pressSlot = null;
            if (_drag is not null && _drag.Slot == slot) EndDrag(commit: true);
            wrapper.ReleaseMouseCapture();
        };
    }

    private void BeginDrag(SlotInfo slot)
    {
        var zone = _zones[slot.ZoneIndex];
        int sourceIndex = zone.Entries.IndexOf(slot.Entry);
        if (sourceIndex < 0) return;

        bool animate = SystemParameters.ClientAreaAnimation;
        var wrapper = slot.Wrapper;
        double width = Math.Max(wrapper.ActualWidth, 1);
        double height = Math.Max(wrapper.ActualHeight, 1);
        var screenCenter = wrapper.PointToScreen(new Point(width / 2, height / 2));

        // Sem um jeito genérico de "recriar" o visual de um widget qualquer (ao contrário
        // dos ícones do launcher) — renderiza o próprio wrapper num bitmap pro fantasma.
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(wrapper);
        var ghostVisual = new Image { Source = rtb, Width = width, Height = height };

        // Vira um espaço vazio (opacidade 0) guardando o lugar — IsHitTestVisible continua
        // true de propósito, senão o WPF para de rotear MouseMove pra quem tem a captura.
        wrapper.Opacity = 0;

        double slotSize = (_vertical ? height : width)
            + (_vertical ? wrapper.Margin.Top + wrapper.Margin.Bottom : wrapper.Margin.Left + wrapper.Margin.Right);

        var ghost = new GhostIconWindow(ghostVisual, width, height);
        ghost.CenterOn(screenCenter);
        ghost.Show();
        ghost.AnimatePickup(animate, LauncherDragAnimations.PickupScale);

        _drag = new DragState
        {
            Slot = slot,
            Ghost = ghost,
            OriginZone = slot.ZoneIndex,
            OriginIndex = sourceIndex,
            OriginalScreenCenter = screenCenter,
            Animate = animate,
            SlotSize = slotSize,
            CurrentZone = slot.ZoneIndex,
            CurrentTarget = sourceIndex,
            Others = BuildSiblings(zone.Panel, wrapper)
        };
        _escapeWatchTimer.Start();
    }

    private void UpdateDrag(MouseEventArgs e)
    {
        if (_drag is null) return;
        var drag = _drag;

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0)
        {
            EndDrag(commit: false);
            return;
        }

        var screenPoint = drag.Slot.Wrapper.PointToScreen(e.GetPosition(drag.Slot.Wrapper));
        drag.Ghost.CenterOn(screenPoint);

        int zoneIndex = FindZoneAt(screenPoint);
        if (zoneIndex != drag.CurrentZone) SwitchZone(drag, zoneIndex);

        var zone = _zones[drag.CurrentZone];
        var pointInZone = zone.Panel.PointFromScreen(screenPoint);
        double clickPos = _vertical ? pointInZone.Y : pointInZone.X;

        int target = 0;
        for (int i = 0; i < drag.Others.Count; i++)
        {
            var sibling = drag.Others[i];
            if (clickPos >= sibling.OriginalOffset + sibling.Size / 2) target = i + 1;
        }
        drag.CurrentTarget = target;

        // Na zona de origem, os vizinhos antes/depois da posição original do item
        // arrastado se comportam como no launcher; numa zona diferente ele nunca esteve
        // lá, então é só "abrir espaço" a partir do alvo (equivalente a origem = fim).
        int originIndexForShift = drag.CurrentZone == drag.OriginZone ? drag.OriginIndex : drag.Others.Count;

        for (int i = 0; i < drag.Others.Count; i++)
        {
            bool isBefore = i < originIndexForShift;
            int shift = isBefore ? (i >= target ? 1 : 0) : (i >= target ? 0 : -1);
            double offsetPx = shift * drag.SlotSize;
            AnimateTranslate(drag.Others[i].Transform, _vertical ? 0 : offsetPx, _vertical ? offsetPx : 0, drag.Animate);
        }
    }

    private void SwitchZone(DragState drag, int newZoneIndex)
    {
        foreach (var sibling in drag.Others) AnimateTranslate(sibling.Transform, 0, 0, drag.Animate);
        drag.CurrentZone = newZoneIndex;
        drag.Others = BuildSiblings(_zones[newZoneIndex].Panel, drag.Slot.Wrapper);
    }

    /// <summary>Divide a barra em 3 terços (início/centro/fim) — necessário porque as 3 zonas ficam sobrepostas na mesma célula e uma zona vazia não tem área própria pra apontar o cursor.</summary>
    private int FindZoneAt(Point screenPoint)
    {
        var local = _zonesRoot.PointFromScreen(screenPoint);
        double total = _vertical ? _zonesRoot.ActualHeight : _zonesRoot.ActualWidth;
        if (total <= 0) return 1;

        double pos = _vertical ? local.Y : local.X;
        double third = total / 3.0;
        if (pos < third) return 0;
        if (pos < third * 2) return 1;
        return 2;
    }

    private List<SiblingInfo> BuildSiblings(StackPanel panel, FrameworkElement exclude)
    {
        var others = new List<SiblingInfo>();
        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            if (child == exclude || !_wrappers.Contains(child)) continue;

            var offset = child.TranslatePoint(new Point(0, 0), panel);
            var transform = new TranslateTransform();
            child.RenderTransform = transform;
            others.Add(new SiblingInfo
            {
                Element = child,
                OriginalOffset = _vertical ? offset.Y : offset.X,
                Size = _vertical ? child.ActualHeight : child.ActualWidth,
                Transform = transform
            });
        }
        return others;
    }

    private void EndDrag(bool commit)
    {
        if (_drag is null) return;
        var drag = _drag;
        _drag = null;
        _escapeWatchTimer.Stop();
        drag.Slot.Wrapper.ReleaseMouseCapture();
        drag.Slot.Wrapper.Cursor = Cursors.Arrow;

        bool moved = drag.CurrentZone != drag.OriginZone || drag.CurrentTarget != drag.OriginIndex;
        if (commit && moved)
        {
            var targetCenter = ComputeTargetScreenCenter(drag);
            drag.Ghost.FlyTo(targetCenter, drag.Animate, () =>
            {
                drag.Ghost.Close();
                _zones[drag.OriginZone].Entries.Remove(drag.Slot.Entry);
                var destEntries = _zones[drag.CurrentZone].Entries;
                int insertAt = Math.Clamp(drag.CurrentTarget, 0, destEntries.Count);
                destEntries.Insert(insertAt, drag.Slot.Entry);
                ConfigService.Save(_cfg);

                // Só reordena os elementos já existentes nas zonas afetadas — nada de
                // App.Reload() aqui, que destruiria e recriaria a BarWindow inteira (e
                // com ela os IWidget, reiniciando o DispatcherTimer do relógio, a sessão
                // do SMTC do widget de mídia, etc.) só pra mover um widget de lugar.
                RebuildZoneChildren(drag.OriginZone);
                if (drag.CurrentZone != drag.OriginZone) RebuildZoneChildren(drag.CurrentZone);
            });
            return;
        }

        // Cancelado (Esc) ou soltou sem mudar de lugar: volta tudo animado, sem tocar no config.
        foreach (var sibling in drag.Others) AnimateTranslate(sibling.Transform, 0, 0, drag.Animate);
        drag.Ghost.FlyTo(drag.OriginalScreenCenter, drag.Animate, () =>
        {
            drag.Ghost.Close();
            drag.Slot.Wrapper.Opacity = 1;
        });
    }

    /// <summary>
    /// Reconstrói a ordem dos filhos de uma zona a partir da lista de entradas já
    /// atualizada — remove tudo e recoloca os mesmos wrappers (nunca cria um IWidget
    /// novo) com separadores frescos entre eles. Zona Start ganha um separador extra no
    /// fim (voltado pro centro) e End um no começo, igual ao WidgetZoneBuilder.Build();
    /// Center não tem separador de zona.
    /// </summary>
    private void RebuildZoneChildren(int zoneIndex)
    {
        var zone = _zones[zoneIndex];
        zone.Panel.Children.Clear();

        bool hasAny = zone.Entries.Count > 0;
        bool leadingBoundary = zoneIndex == 2; // End
        bool trailingBoundary = zoneIndex == 0; // Start

        if (leadingBoundary && hasAny) zone.Panel.Children.Add(_createSeparator());

        for (int i = 0; i < zone.Entries.Count; i++)
        {
            if (i > 0) zone.Panel.Children.Add(_createSeparator());

            var wrapper = _wrapperByEntry[zone.Entries[i]];
            wrapper.RenderTransform = null;
            wrapper.Opacity = 1;
            zone.Panel.Children.Add(wrapper);
        }

        if (trailingBoundary && hasAny) zone.Panel.Children.Add(_createSeparator());
    }

    /// <summary>Centro de tela de onde o item vai parar se soltar agora — pro fantasma "voar" até lá antes de reordenar.</summary>
    private Point ComputeTargetScreenCenter(DragState drag)
    {
        var panel = _zones[drag.CurrentZone].Panel;
        var panelTopLeft = panel.PointToScreen(new Point(0, 0));

        if (drag.Others.Count == 0)
        {
            return _vertical
                ? new Point(panelTopLeft.X + Math.Max(panel.ActualWidth, drag.SlotSize) / 2, panelTopLeft.Y + drag.SlotSize / 2)
                : new Point(panelTopLeft.X + drag.SlotSize / 2, panelTopLeft.Y + Math.Max(panel.ActualHeight, drag.SlotSize) / 2);
        }

        double targetOffset = drag.CurrentTarget < drag.Others.Count
            ? drag.Others[drag.CurrentTarget].OriginalOffset
            : drag.Others[^1].OriginalOffset + drag.Others[^1].Size;
        targetOffset += drag.SlotSize / 2;

        return _vertical
            ? new Point(panelTopLeft.X + panel.ActualWidth / 2, panelTopLeft.Y + targetOffset)
            : new Point(panelTopLeft.X + targetOffset, panelTopLeft.Y + panel.ActualHeight / 2);
    }

    private static void AnimateTranslate(TranslateTransform transform, double x, double y, bool animate)
    {
        if (!animate)
        {
            transform.X = x;
            transform.Y = y;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(LauncherDragAnimations.SlideMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, duration) { EasingFunction = ease });
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, duration) { EasingFunction = ease });
    }
}
