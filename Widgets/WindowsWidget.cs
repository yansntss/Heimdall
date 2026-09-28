using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;

namespace Heimdall.Widgets;

/// <summary>
/// Lista automaticamente as janelas de topo abertas no momento — uma taskbar leve
/// dentro do Heimdall, sem precisar fixar nada como atalho. Reage em tempo real via
/// SetWinEventHook (uma lista que demora segundos pra refletir uma janela nova/fechada
/// fica visivelmente errada — diferente do indicador do launcher, aqui o hook se
/// justifica). Fora de escopo de propósito: miniaturas (DwmRegisterThumbnail) e jump list.
/// </summary>
public sealed class WindowsWidget : IWidget
{
    private const int IconMargin = 6;
    private const int MinIconSize = 16;
    private const int MaxIconSize = 48;
    private const int RebuildDebounceMs = 150;
    private const int HoverMs = 150;
    private const int PopupOpenDelayMs = 300;
    private const int PopupCloseDelayMs = 250;

    private readonly WindowsWidgetConfig _wcfg;
    private readonly EffectiveStyle _style;
    private readonly int _iconSize;
    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center };

    // Burst de eventos (ex.: vários EVENT_OBJECT_DESTROY seguidos ao fechar um app com
    // várias janelas) vira um Rebuild() só, debounced, em vez de reenumerar tudo a cada evento.
    private readonly DispatcherTimer _rebuildDebounce;

    // Delegates mantidos vivos em campos: SetWinEventHook não segura referência gerenciada
    // pro callback — sem isso, o GC coletaria o delegate e o hook chamaria memória já livre.
    private readonly NativeMethods.WinEventProc _onShowOrDestroy;
    private readonly NativeMethods.WinEventProc _onForeground;
    private readonly NativeMethods.WinEventProc _onNameChange;
    private IntPtr _hookShowOrDestroy;
    private IntPtr _hookForeground;
    private IntPtr _hookNameChange;

    private readonly List<ItemState> _items = new();
    private Popup? _openPopup;

    // Abrir no hover precisa de um atraso (senão pisca ao só passar o mouse de raspão) e
    // fechar também (senão fecha ao atravessar o gap entre o ícone e o popup) — os dois
    // timers rodam um de cada vez, nunca junto (Schedule* sempre para o outro primeiro).
    private readonly DispatcherTimer _popupOpenTimer;
    private readonly DispatcherTimer _popupCloseTimer;
    private FrameworkElement? _pendingPopupAnchor;
    private IReadOnlyList<WindowTrackerService.TrackedWindow>? _pendingPopupWindows;

    public FrameworkElement View => _root;

    public WindowsWidget(AppConfig cfg)
    {
        _wcfg = cfg.Windows;
        _style = ThemeService.GetEffectiveStyle(cfg);
        _iconSize = Math.Clamp(cfg.Thickness - IconMargin * 2, MinIconSize, MaxIconSize);

        _rebuildDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RebuildDebounceMs) };
        _rebuildDebounce.Tick += (_, _) =>
        {
            // Outro app qualquer trocando de título com frequência (spinner de carregamento,
            // cronômetro etc.) dispara NAMECHANGE sem parar — sem isso, um Rebuild() no meio
            // do hover destrói o ícone embaixo do cursor e recria na mesma hora, reabrindo o
            // popup, que dispara SHOW de novo, e por aí vai: o "piscando". Enquanto o mouse
            // está em cima do widget (ou o popup aberto), só reagenda — aplica assim que a
            // interação acabar, em vez de interromper na hora.
            if (_root.IsMouseOver || _openPopup is not null)
            {
                _rebuildDebounce.Stop();
                _rebuildDebounce.Start();
                return;
            }
            _rebuildDebounce.Stop();
            Rebuild();
        };

        _popupOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PopupOpenDelayMs) };
        _popupOpenTimer.Tick += (_, _) =>
        {
            _popupOpenTimer.Stop();
            if (_pendingPopupAnchor is not null && _pendingPopupWindows is not null)
                ShowWindowListPopup(_pendingPopupAnchor, _pendingPopupWindows);
        };
        _popupCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PopupCloseDelayMs) };
        _popupCloseTimer.Tick += (_, _) => { _popupCloseTimer.Stop(); ClosePopup(); };

        _onShowOrDestroy = (_, _, hwnd, idObject, idChild, _, _) =>
        {
            if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF) return;
            // O hook é system-wide: sem isso, o próprio popup de hover (uma janela nova)
            // dispara o próprio SHOW, reconstrói a lista, fecha o popup que acabou de
            // abrir — e como o mouse continua parado em cima, reabre e dispara de novo,
            // um loop infinito de abre/fecha (o "piscando").
            if (IsOwnProcessWindow(hwnd)) return;
            ScheduleRebuild();
        };
        _onNameChange = (_, _, hwnd, idObject, idChild, _, _) =>
        {
            if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF) return;
            if (IsOwnProcessWindow(hwnd)) return;
            ScheduleRebuild();
        };
        // Só destaque, sem reconstruir a lista inteira: alt-tab é frequente e não muda
        // quais janelas existem, só qual está em foco.
        _onForeground = (_, _, _, _, _, _, _) => RefreshFocusHighlight();
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start()
    {
        Rebuild();

        // WINEVENT_OUTOFCONTEXT com idProcess/idThread = 0: hook system-wide (qualquer
        // processo), entregue via fila de mensagens desta própria thread — que já tem
        // message pump (Dispatcher do WPF), sem precisar de DLL injetada nos outros processos.
        _hookShowOrDestroy = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_SHOW,
            IntPtr.Zero, _onShowOrDestroy, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        _hookForeground = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _onForeground, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        _hookNameChange = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_NAMECHANGE, NativeMethods.EVENT_OBJECT_NAMECHANGE,
            IntPtr.Zero, _onNameChange, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void ScheduleRebuild()
    {
        _rebuildDebounce.Stop();
        _rebuildDebounce.Start();
    }

    private static bool IsOwnProcessWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    // ---------- Abrir/fechar o popup de lista ao passar o mouse (hover), não só no clique ----------

    private void ScheduleShowPopup(FrameworkElement anchor, IReadOnlyList<WindowTrackerService.TrackedWindow> windows)
    {
        _popupCloseTimer.Stop();

        // Já aberto pra esse mesmo item (voltou o mouse antes do fechamento disparar) —
        // nada a fazer, sem reabrir/piscar.
        if (_openPopup is not null && _pendingPopupAnchor == anchor) return;

        _pendingPopupAnchor = anchor;
        _pendingPopupWindows = windows;
        _popupOpenTimer.Stop();
        _popupOpenTimer.Start();
    }

    private void ScheduleHidePopup()
    {
        _popupOpenTimer.Stop();
        if (_openPopup is null) return;
        _popupCloseTimer.Stop();
        _popupCloseTimer.Start();
    }

    // ---------- Modelo: um item da barra = 1 app com 1+ janelas (grupo ou não, mesma forma) ----------

    private sealed class AppItem
    {
        public required string ExecutablePath;
        public required List<WindowTrackerService.TrackedWindow> Windows;
    }

    private sealed class ItemState
    {
        public required Border Element;
        public required SolidColorBrush Background;
        public required List<IntPtr> Handles;
        public bool Focused;
    }

    private void Rebuild()
    {
        ClosePopup();
        _root.Children.Clear();
        _items.Clear();

        var excluded = new HashSet<string>(_wcfg.ExcludeProcesses, StringComparer.OrdinalIgnoreCase);
        var windows = WindowTrackerService.SnapshotAll()
            .Where(w => !excluded.Contains(System.IO.Path.GetFileName(w.ExecutablePath)))
            .ToList();

        List<AppItem> items = _wcfg.GroupWindows
            ? GroupByExecutable(windows)
            : windows.Select(w => new AppItem { ExecutablePath = w.ExecutablePath, Windows = new List<WindowTrackerService.TrackedWindow> { w } }).ToList();

        int maxItems = Math.Max(1, _wcfg.MaxItems);
        List<AppItem> visible = items;
        List<WindowTrackerService.TrackedWindow>? overflow = null;

        if (items.Count > maxItems)
        {
            visible = items.Take(maxItems - 1).ToList();
            overflow = items.Skip(maxItems - 1).SelectMany(i => i.Windows).ToList();
        }

        foreach (var item in visible)
            _root.Children.Add(CreateItem(item));

        if (overflow is { Count: > 0 })
            _root.Children.Add(CreateOverflowItem(overflow));

        RefreshFocusHighlight();
    }

    private static List<AppItem> GroupByExecutable(List<WindowTrackerService.TrackedWindow> windows)
    {
        var groups = new List<AppItem>();
        var index = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var window in windows)
        {
            if (index.TryGetValue(window.ExecutablePath, out var group))
            {
                group.Windows.Add(window);
            }
            else
            {
                group = new AppItem { ExecutablePath = window.ExecutablePath, Windows = new List<WindowTrackerService.TrackedWindow> { window } };
                index[window.ExecutablePath] = group;
                groups.Add(group);
            }
        }

        return groups;
    }

    // ---------- Item da barra ----------

    private Border CreateItem(AppItem item)
    {
        var iconVisual = ResolveIconVisual(item.ExecutablePath, _iconSize);

        var overlay = new Grid();
        overlay.Children.Add(iconVisual);
        overlay.Children.Add(item.Windows.Count > 1 ? CreateCountBadge(item.Windows.Count) : CreateDot());

        FrameworkElement content = overlay;
        if (!_wcfg.GroupWindows || _wcfg.ShowTitles)
        {
            var label = new TextBlock
            {
                Text = GetDisplayLabel(item),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                Foreground = new SolidColorBrush(_style.Foreground),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 160
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(overlay);
            row.Children.Add(label);
            content = row;
        }

        var background = new SolidColorBrush(Colors.Transparent);
        var border = new Border
        {
            Child = content,
            Background = background,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(IconMargin / 2.0),
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = Cursors.Hand,
            // Num grupo, o próprio popup de hover já mostra os títulos — um tooltip nativo
            // por cima ficaria redundante (e os dois disputando espaço/timing).
            ToolTip = item.Windows.Count == 1 ? BuildTooltip(item) : null
        };

        var state = new ItemState { Element = border, Background = background, Handles = item.Windows.Select(w => w.Handle).ToList() };
        _items.Add(state);

        border.MouseEnter += (_, _) =>
        {
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_style.Hover, TimeSpan.FromMilliseconds(HoverMs)));
            if (item.Windows.Count > 1) ScheduleShowPopup(border, item.Windows);
        };
        border.MouseLeave += (_, _) =>
        {
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(BaseColor(state), TimeSpan.FromMilliseconds(HoverMs)));
            if (item.Windows.Count > 1) ScheduleHidePopup();
        };

        border.MouseLeftButtonUp += (_, _) =>
        {
            if (item.Windows.Count == 1) ToggleFocus(item.Windows[0].Handle);
            else ShowWindowListPopup(border, item.Windows);
        };

        border.ContextMenu = BuildContextMenu(item);

        return border;
    }

    private Border CreateOverflowItem(List<WindowTrackerService.TrackedWindow> overflow)
    {
        var badge = new TextBlock
        {
            Text = $"+{overflow.Count}",
            FontSize = Math.Max(10, _iconSize * 0.4),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_style.Foreground),
            Width = _iconSize,
            Height = _iconSize,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var background = new SolidColorBrush(Colors.Transparent);
        var border = new Border
        {
            Child = badge,
            Background = background,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(IconMargin / 2.0),
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = Cursors.Hand,
            ToolTip = Strings.WindowsOverflowTooltip(overflow.Count)
        };

        border.MouseEnter += (_, _) =>
        {
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_style.Hover, TimeSpan.FromMilliseconds(HoverMs)));
            ScheduleShowPopup(border, overflow);
        };
        border.MouseLeave += (_, _) =>
        {
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(HoverMs)));
            ScheduleHidePopup();
        };
        border.MouseLeftButtonUp += (_, _) => ShowWindowListPopup(border, overflow);

        return border;
    }

    // ---------- Destaque da janela em foco ----------

    private void RefreshFocusHighlight()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        foreach (var state in _items)
        {
            state.Focused = state.Handles.Contains(foreground);
            // Não pisa na animação de hover enquanto o mouse está em cima do item.
            if (state.Element.IsMouseOver) continue;
            state.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(BaseColor(state), TimeSpan.FromMilliseconds(HoverMs)));
        }
    }

    private Color BaseColor(ItemState state) => state.Focused
        ? Color.FromArgb(0x33, _style.Accent.R, _style.Accent.G, _style.Accent.B)
        : Colors.Transparent;

    // ---------- Clique / menu de contexto ----------

    private static void ToggleFocus(IntPtr hwnd)
    {
        if (WindowTrackerService.IsForeground(hwnd)) WindowTrackerService.Minimize(hwnd);
        else WindowTrackerService.FocusOrRestore(hwnd);
    }

    private ContextMenu BuildContextMenu(AppItem item)
    {
        // Num grupo com várias janelas, as ações se aplicam a todas de uma vez — não há
        // como escolher uma janela específica sem antes abrir o popup de lista.
        var close = new MenuItem { Header = Strings.WindowsCloseWindow };
        close.Click += (_, _) => { foreach (var w in item.Windows) WindowTrackerService.Close(w.Handle); };

        var minimize = new MenuItem { Header = Strings.WindowsMinimize };
        minimize.Click += (_, _) => { foreach (var w in item.Windows) WindowTrackerService.Minimize(w.Handle); };

        var pin = new MenuItem { Header = Strings.WindowsPinAsShortcut };
        pin.Click += (_, _) => ((App)Application.Current).AddLauncher(new LauncherConfig
        {
            Name = AppDisplayName(item.ExecutablePath),
            Path = item.ExecutablePath
        });

        return new ContextMenu { Items = { close, minimize, new Separator(), pin } };
    }

    // ---------- Popup de lista (grupo com várias janelas, ou o "+N" de excedentes) ----------

    private void ShowWindowListPopup(FrameworkElement anchor, IReadOnlyList<WindowTrackerService.TrackedWindow> windows)
    {
        ClosePopup();
        _popupOpenTimer.Stop();
        _pendingPopupAnchor = anchor;
        _pendingPopupWindows = windows;

        var panel = new StackPanel();
        foreach (var window in windows)
        {
            var icon = ResolveIconVisual(window.ExecutablePath, 16);

            var text = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(window.Title) ? Strings.WindowsUntitled : window.Title,
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 220
            };

            var closeIcon = new TextBlock
            {
                Text = "✕",
                FontSize = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(_style.Foreground)
            };
            var closeButton = new Button
            {
                Content = closeIcon,
                Opacity = 0,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                ToolTip = Strings.WindowsCloseWindow
            };

            // Grid (não StackPanel): a 3ª coluna com largura Auto fica encostada na borda
            // direita do popup mesmo com títulos curtos — StackPanel deixaria o botão colado
            // no fim do texto, variando de lugar a cada linha.
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(icon, 0);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(closeButton, 2);
            row.Children.Add(icon);
            row.Children.Add(text);
            row.Children.Add(closeButton);

            var rowBackground = new SolidColorBrush(Colors.Transparent);
            var rowBorder = new Border
            {
                Child = row,
                Background = rowBackground,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 5, 8, 5),
                Cursor = Cursors.Hand
            };

            rowBorder.MouseEnter += (_, _) =>
            {
                rowBackground.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_style.Hover, TimeSpan.FromMilliseconds(HoverMs)));
                closeButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(HoverMs)));
            };
            rowBorder.MouseLeave += (_, _) =>
            {
                rowBackground.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(HoverMs)));
                closeButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(HoverMs)));
            };
            rowBorder.MouseLeftButtonUp += (_, _) =>
            {
                WindowTrackerService.FocusOrRestore(window.Handle);
                ClosePopup();
            };

            closeButton.Click += (_, e) =>
            {
                e.Handled = true;
                WindowTrackerService.Close(window.Handle);
                panel.Children.Remove(rowBorder);
                if (panel.Children.Count == 0) ClosePopup();
            };

            panel.Children.Add(rowBorder);
        }

        var border = new Border
        {
            Background = new SolidColorBrush(_style.Background),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, _style.Foreground.R, _style.Foreground.G, _style.Foreground.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4),
            Child = panel
        };
        TextElement.SetForeground(border, new SolidColorBrush(_style.Foreground));

        // Mouse passando do ícone pro popup cruza um gap sem elemento nenhum embaixo —
        // sem isso, o MouseLeave do ícone já agendaria o fechamento e o popup sumiria
        // antes do cursor chegar nas linhas dele.
        border.MouseEnter += (_, _) => _popupCloseTimer.Stop();
        border.MouseLeave += (_, _) => ScheduleHidePopup();

        _openPopup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = anchor,
            // true: StaysOpen=false fecha quando a janela dona "desativa" — mas a barra é
            // WS_EX_NOACTIVATE e nunca ativa de verdade, então o popup se fechava sozinho
            // na mesma hora que abria. Fechar por conta própria via hover (ScheduleHidePopup)
            // e pelo clique num item, que já chamam ClosePopup() explicitamente.
            StaysOpen = true,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = border
        };
        _openPopup.Closed += (_, _) => _openPopup = null;
        _openPopup.IsOpen = true;
    }

    private void ClosePopup()
    {
        _popupOpenTimer.Stop();
        _popupCloseTimer.Stop();
        _pendingPopupAnchor = null;
        _pendingPopupWindows = null;
        if (_openPopup is null) return;
        _openPopup.IsOpen = false;
        _openPopup = null;
    }

    // ---------- Visual / texto ----------

    private FrameworkElement ResolveIconVisual(string exePath, double size)
    {
        var source = IconCacheService.GetOrExtract(exePath, (int)size);
        if (source is not null)
            return new Image { Width = size, Height = size, Stretch = Stretch.Uniform, Source = source };

        // Sem ícone extraído (processo protegido, caminho quebrado) — um glifo genérico
        // no lugar da imagem em branco, senão o item some visualmente da barra.
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(2),
            BorderBrush = new SolidColorBrush(_style.Foreground),
            BorderThickness = new Thickness(1)
        };
    }

    private FrameworkElement CreateDot() => new Ellipse
    {
        Width = 4,
        Height = 4,
        Fill = new SolidColorBrush(_style.Accent),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(0, 0, 0, 1)
    };

    private FrameworkElement CreateCountBadge(int count) => new Border
    {
        Background = new SolidColorBrush(_style.Accent),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(3, 0, 3, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(0, 0, -2, -2),
        Child = new TextBlock
        {
            Text = count.ToString(),
            FontSize = 8,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        }
    };

    private static string AppDisplayName(string exePath) => System.IO.Path.GetFileNameWithoutExtension(exePath);

    private static string GetDisplayLabel(AppItem item)
    {
        if (item.Windows.Count == 1)
        {
            string title = item.Windows[0].Title;
            return string.IsNullOrWhiteSpace(title) ? AppDisplayName(item.ExecutablePath) : title;
        }
        return AppDisplayName(item.ExecutablePath);
    }

    private static string BuildTooltip(AppItem item) =>
        string.Join("\n", item.Windows.Select(w => string.IsNullOrWhiteSpace(w.Title) ? Strings.WindowsUntitled : w.Title));

    public void Dispose()
    {
        _rebuildDebounce.Stop();
        _popupOpenTimer.Stop();
        _popupCloseTimer.Stop();
        ClosePopup();

        if (_hookShowOrDestroy != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookShowOrDestroy);
        if (_hookForeground != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookForeground);
        if (_hookNameChange != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookNameChange);
    }
}
