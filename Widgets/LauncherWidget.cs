using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;
using Heimdall.UI;

namespace Heimdall.Widgets;

/// <summary>Ícones de atalhos (executáveis, .lnk, pastas, arquivos, URLs, apps da Store) lado a lado ou empilhados.</summary>
public sealed class LauncherWidget : IWidget
{
    private const int IconMargin = 6;
    private const int MinIconSize = 16;
    private const int MaxIconSize = 48;

    /// <summary>Distância (px) além dos limites da barra pra armar o indicador de remoção ao arrastar pra longe.</summary>
    private const double RemovalDistance = 50;

    private readonly AppConfig _cfg;
    private readonly EffectiveStyle _style;
    private readonly int _iconSize;
    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _runningTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly List<(string TargetPath, Ellipse Dot, LauncherConfig Launcher)> _runningIndicators = new();

    /// <summary>Launcher → HWND da janela aberta encontrada na última varredura — usado pro clique focar em vez de abrir de novo.</summary>
    private readonly Dictionary<LauncherConfig, IntPtr> _openWindows = new();
    // Esc só é lido de dentro do PreviewMouseMove (via UpdateDrag) — sem isso, segurar o
    // mouse parado e apertar Esc nunca cancelava, já que nada disparava a checagem.
    private readonly DispatcherTimer _escapeWatchTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };

    public FrameworkElement View => _root;

    public LauncherWidget(AppConfig cfg)
    {
        _cfg = cfg;
        _style = ThemeService.GetEffectiveStyle(cfg);
        _iconSize = Math.Clamp(cfg.Thickness - IconMargin * 2, MinIconSize, MaxIconSize);
        _runningTimer.Tick += (_, _) => CheckRunning();
        _escapeWatchTimer.Tick += (_, _) =>
        {
            if (_drag is not null && (NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0)
                EndDrag(commit: false);
        };
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start()
    {
        Rebuild();
        _runningTimer.Start();
        CheckRunning();
    }

    private void Rebuild()
    {
        _root.Children.Clear();
        _runningIndicators.Clear();
        _openWindows.Clear();
        foreach (var launcher in _cfg.Launchers)
            _root.Children.Add(launcher.Type == LauncherItemType.Separator ? CreateSeparatorItem(launcher) : CreateIcon(launcher));
    }

    /// <summary>Índice em <see cref="AppConfig.Launchers"/> correspondente a um ponto (nas coordenadas do <see cref="View"/>) — usado pelo "Adicionar separador" da barra pra inserir na posição do clique.</summary>
    public int GetInsertIndex(Point pointInView)
    {
        double clickPos = _root.Orientation == Orientation.Horizontal ? pointInView.X : pointInView.Y;

        for (int i = 0; i < _root.Children.Count; i++)
        {
            if (_root.Children[i] is not FrameworkElement child) continue;

            var topLeft = child.TranslatePoint(new Point(0, 0), _root);
            double center = _root.Orientation == Orientation.Horizontal
                ? topLeft.X + child.ActualWidth / 2
                : topLeft.Y + child.ActualHeight / 2;
            if (clickPos < center) return i;
        }

        return _root.Children.Count;
    }

    // ---------- Ponto indicando que o app já está aberto ----------

    /// <summary>
    /// Varre as janelas de topo "reais" a cada poucos segundos — só pra atalhos locais
    /// (exe/.lnk), não dá pra checar apps da Store/URLs assim. Guarda o HWND encontrado
    /// pra cada launcher, usado pelo clique focar em vez de abrir de novo.
    /// </summary>
    private void CheckRunning()
    {
        if (_runningIndicators.Count == 0) return;

        var openWindows = OpenWindowsService.Snapshot();
        foreach (var (targetPath, dot, launcher) in _runningIndicators)
        {
            if (openWindows.TryGetValue(targetPath, out var hwnd))
            {
                _openWindows[launcher] = hwnd;
                dot.Visibility = Visibility.Visible;
            }
            else
            {
                _openWindows.Remove(launcher);
                dot.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>Só o visual do ícone (imagem ou glifo de fallback) — reaproveitado pelo ícone real e pelo fantasma do arraste.</summary>
    private FrameworkElement CreateIconVisual(LauncherConfig launcher, bool exists)
    {
        var iconSource = exists ? ResolveIconSource(launcher) : null;

        // Sem ícone extraído (caminho quebrado, ou falha na extração) — glifo genérico no
        // lugar da imagem em branco, senão o atalho "quebrado" fica invisível na barra.
        if (iconSource is not null)
            return new Image { Width = _iconSize, Height = _iconSize, Stretch = Stretch.Uniform, Source = iconSource };

        {
            var glyph = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = _iconSize * 0.6,
                Foreground = new SolidColorBrush(_style.Foreground),
                Width = _iconSize,
                Height = _iconSize,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextOptions.SetTextRenderingMode(glyph, TextRenderingMode.Grayscale);
            TextOptions.SetTextFormattingMode(glyph, TextFormattingMode.Display);
            return glyph;
        }
    }

    /// <summary>Visual de um item (ícone de app ou traço de separador) sem moldura/interação — usado pro fantasma do arraste.</summary>
    private FrameworkElement CreateItemVisual(LauncherConfig item) =>
        item.Type == LauncherItemType.Separator ? CreateSeparatorVisual(item) : CreateIconVisual(item, PathExists(item.Path));

    private Border CreateIcon(LauncherConfig launcher)
    {
        bool exists = PathExists(launcher.Path);
        FrameworkElement content = CreateIconVisual(launcher, exists);

        if (TryGetLocalTargetPath(launcher) is { } targetPath)
        {
            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(_style.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1),
                Visibility = Visibility.Collapsed
            };
            var withDot = new Grid();
            withDot.Children.Add(content);
            withDot.Children.Add(dot);
            content = withDot;
            _runningIndicators.Add((targetPath, dot, launcher));
        }

        var background = new SolidColorBrush(Colors.Transparent);
        var border = new Border
        {
            Child = content,
            Background = background,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(IconMargin / 2.0),
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = exists ? Cursors.Hand : Cursors.Arrow,
            Opacity = exists ? 1.0 : 0.35,
            ToolTip = exists ? launcher.Name : Strings.LauncherTooltipMissing(launcher.Name)
        };

        border.MouseEnter += (_, _) =>
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_style.Hover, TimeSpan.FromMilliseconds(150)));
        border.MouseLeave += (_, _) =>
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(150)));

        border.MouseLeftButtonUp += (_, _) =>
        {
            if (_openWindows.TryGetValue(launcher, out var hwnd)) OpenWindowsService.FocusOrRestore(hwnd);
            else Launch(launcher, forceAdmin: false);
        };

        SetupItemInteraction(border, launcher);
        border.ContextMenu = BuildContextMenu(launcher, border);

        return border;
    }

    // ---------- Separadores dentro da lista de atalhos ----------

    private FrameworkElement CreateSeparatorVisual(LauncherConfig separator)
    {
        bool vertical = _root.Orientation == Orientation.Vertical;
        double lineThickness = Math.Max(1, _cfg.Thickness * 0.6);
        var lineBrush = new SolidColorBrush(_style.Border);

        FrameworkElement visual = separator.Style switch
        {
            SeparatorStyle.Dot => new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = lineBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            SeparatorStyle.Space => new Border
            {
                Width = vertical ? lineThickness : 12,
                Height = vertical ? 12 : lineThickness
            },
            _ => new Border
            {
                Background = lineBrush,
                Width = vertical ? lineThickness : 1,
                Height = vertical ? 1 : lineThickness,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        return visual;
    }

    private Border CreateSeparatorItem(LauncherConfig separator)
    {
        bool vertical = _root.Orientation == Orientation.Vertical;
        var visual = CreateSeparatorVisual(separator);

        // Padding generoso: uma linha de 1px só, sem isso, vira um alvo minúsculo demais
        // pra hover/clique direito confortável — a área clicável fica do tamanho de um
        // ícone mesmo, só o traço visível no meio que é fino.
        double pad = Math.Max(4, (_iconSize - 1) / 2.0);
        var background = new SolidColorBrush(Colors.Transparent);
        var container = new Border
        {
            Child = visual,
            Background = background,
            CornerRadius = new CornerRadius(4),
            Padding = vertical ? new Thickness(4, pad, 4, pad) : new Thickness(pad, 4, pad, 4),
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = Cursors.Arrow
        };

        container.MouseEnter += (_, _) =>
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_style.Hover, TimeSpan.FromMilliseconds(150)));
        container.MouseLeave += (_, _) =>
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(150)));

        SetupItemInteraction(container, separator);
        container.ContextMenu = BuildSeparatorContextMenu(separator);

        return container;
    }

    private ContextMenu BuildSeparatorContextMenu(LauncherConfig separator)
    {
        var line = new MenuItem { Header = Strings.SeparatorStyleLine, IsCheckable = true, IsChecked = separator.Style == SeparatorStyle.Line };
        var space = new MenuItem { Header = Strings.SeparatorStyleSpace, IsCheckable = true, IsChecked = separator.Style == SeparatorStyle.Space };
        var dot = new MenuItem { Header = Strings.SeparatorStyleDot, IsCheckable = true, IsChecked = separator.Style == SeparatorStyle.Dot };
        line.Click += (_, _) => ChangeSeparatorStyle(separator, SeparatorStyle.Line);
        space.Click += (_, _) => ChangeSeparatorStyle(separator, SeparatorStyle.Space);
        dot.Click += (_, _) => ChangeSeparatorStyle(separator, SeparatorStyle.Dot);

        var remove = new MenuItem { Header = Strings.Remove };
        remove.Click += (_, _) => RemoveLauncher(separator);

        return new ContextMenu { Items = { line, space, dot, new Separator(), remove } };
    }

    private void ChangeSeparatorStyle(LauncherConfig separator, SeparatorStyle style)
    {
        separator.Style = style;
        ConfigService.Save(_cfg);
        Rebuild();
    }

    // ---------- Menu de clique direito (ícones de app) ----------

    private ContextMenu BuildContextMenu(LauncherConfig launcher, Border icon)
    {
        var runAsAdmin = new MenuItem { Header = Strings.LauncherRunAsAdmin };
        runAsAdmin.Click += (_, _) => Launch(launcher, forceAdmin: true);

        var openLocation = new MenuItem { Header = Strings.LauncherOpenLocation };
        openLocation.Click += (_, _) => OpenFileLocation(launcher);

        var rename = new MenuItem { Header = Strings.LauncherRename };
        rename.Click += (_, _) => RenameLauncher(launcher, icon);

        var remove = new MenuItem { Header = Strings.Remove };
        remove.Click += (_, _) => RemoveLauncher(launcher);

        return new ContextMenu { Items = { runAsAdmin, openLocation, rename, remove } };
    }

    private void RenameLauncher(LauncherConfig launcher, Border icon)
    {
        var prompt = new RenamePromptWindow(_style, launcher.Name);
        prompt.AnchorTo(icon);
        prompt.Confirmed += newName =>
        {
            launcher.Name = newName;
            ConfigService.Save(_cfg);
            Rebuild();
        };
        prompt.Show();
        prompt.Activate();
    }

    private void RemoveLauncher(LauncherConfig launcher)
    {
        _cfg.Launchers.Remove(launcher);
        ConfigService.Save(_cfg);
        Rebuild();
    }

    private static void OpenFileLocation(LauncherConfig launcher)
    {
        try
        {
            string target = IconCacheService.ResolveTarget(launcher.Path);
            if (File.Exists(target) || Directory.Exists(target))
                Process.Start("explorer.exe", $"/select,\"{target}\"");
        }
        catch
        {
            // Caminho inválido pra selecionar no Explorer — sem crashar por isso.
        }
    }

    // ---------- Arrastar pra reordenar (arraste manual com fantasma animado) ----------

    private sealed class SiblingInfo
    {
        public required FrameworkElement Element;
        public required double OriginalOffset;
        public required double Size;
        public required TranslateTransform Transform;
    }

    private sealed class DragState
    {
        public required LauncherConfig Launcher;
        public required Border SourceElement;
        public required GhostIconWindow Ghost;
        public required int SourceIndex;
        public required double SlotSize;
        public required List<SiblingInfo> Others;
        public required Point OriginalScreenCenter;
        public required bool Animate;
        public required bool Vertical;
        public int CurrentTarget;
        public bool RemovalArmed;
    }

    private Point? _pressPoint;
    private LauncherConfig? _pressSource;
    private DragState? _drag;

    /// <summary>
    /// Detecta clique simples vs. arraste (limiar de alguns pixels) e, uma vez iniciado,
    /// delega pro fantasma. _pressPoint/_pressSource são únicos do widget — sem checar
    /// _pressSource == item, se o cursor passasse por cima de OUTRO ícone antes de cruzar
    /// o limiar, era o ícone errado (o que está embaixo do cursor agora) que começava o
    /// arraste.
    /// </summary>
    private void SetupItemInteraction(Border element, LauncherConfig item)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _pressPoint = e.GetPosition(null);
            _pressSource = item;
            element.CaptureMouse();

            // Essencial: o WidgetDragController escuta o wrapper que envolve o
            // LauncherWidget inteiro (pra poder arrastar o launcher como widget, entre
            // zonas) na fase de bolha (MouseLeftButtonDown, não Preview) — marcar
            // Handled aqui impede que ESSE clique (que é sobre um ícone específico, não
            // sobre o launcher como um todo) borbulhe até lá e dispare os dois arrastes
            // ao mesmo tempo. Sem isso, arrastar um ícone também começava um arraste do
            // widget inteiro por baixo, que escondia (Opacity 0) o wrapper inteiro — daí
            // os outros ícones "sumirem" junto.
            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (_drag is not null && _drag.Launcher == item)
            {
                UpdateDrag(e);
                return;
            }

            if (_pressPoint is null || _pressSource != item || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _pressPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _pressPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            _pressPoint = null;
            _pressSource = null;
            BeginDrag(element, item);
        };

        element.PreviewMouseLeftButtonUp += (_, _) =>
        {
            _pressPoint = null;
            _pressSource = null;
            if (_drag is not null && _drag.Launcher == item) EndDrag(commit: true);
            element.ReleaseMouseCapture();
        };
    }

    private void BeginDrag(Border element, LauncherConfig item)
    {
        int sourceIndex = _cfg.Launchers.IndexOf(item);
        if (sourceIndex < 0) return;

        bool animate = SystemParameters.ClientAreaAnimation;
        bool vertical = _root.Orientation == Orientation.Vertical;

        var screenCenter = element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight / 2));

        // O ícone original vira um espaço vazio (opacidade 0) guardando o lugar, em vez de
        // sumir da lista — o Rebuild() no fim do arraste que efetivamente reordena.
        // IsHitTestVisible continua true de propósito: desligar isso no elemento que
        // segura a captura do mouse fazia o WPF parar de rotear MouseMove pra ele — o
        // arraste "começava" (o fantasma aparecia) mas nunca mais recebia atualização.
        element.Opacity = 0;

        double slotSize = (vertical ? element.ActualHeight : element.ActualWidth)
            + (vertical ? element.Margin.Top + element.Margin.Bottom : element.Margin.Left + element.Margin.Right);

        var others = new List<SiblingInfo>();
        for (int i = 0; i < _root.Children.Count; i++)
        {
            if (i == sourceIndex || _root.Children[i] is not FrameworkElement child) continue;

            var offset = child.TranslatePoint(new Point(0, 0), _root);
            var transform = new TranslateTransform();
            child.RenderTransform = transform;
            others.Add(new SiblingInfo
            {
                Element = child,
                OriginalOffset = vertical ? offset.Y : offset.X,
                Size = vertical ? child.ActualHeight : child.ActualWidth,
                Transform = transform
            });
        }

        var ghostVisual = CreateItemVisual(item);
        var ghost = new GhostIconWindow(ghostVisual, _iconSize);
        ghost.CenterOn(screenCenter);
        ghost.Show();
        ghost.AnimatePickup(animate, LauncherDragAnimations.PickupScale);

        _drag = new DragState
        {
            Launcher = item,
            SourceElement = element,
            Ghost = ghost,
            SourceIndex = sourceIndex,
            SlotSize = slotSize,
            Others = others,
            OriginalScreenCenter = screenCenter,
            Animate = animate,
            Vertical = vertical,
            CurrentTarget = sourceIndex
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

        var screenPoint = _root.PointToScreen(e.GetPosition(_root));
        drag.Ghost.CenterOn(screenPoint);

        // Longe da barra: arma o indicador de remoção e solta os vizinhos de volta ao lugar
        // — não faz sentido calcular posição de reordenar fora dela.
        var rootTopLeft = _root.PointToScreen(new Point(0, 0));
        var barBounds = new Rect(rootTopLeft, new Size(Math.Max(_root.ActualWidth, 1), Math.Max(_root.ActualHeight, 1)));
        barBounds.Inflate(RemovalDistance, RemovalDistance);
        bool removalArmed = !barBounds.Contains(screenPoint);
        drag.RemovalArmed = removalArmed;
        drag.Ghost.SetRemovalHint(removalArmed);

        if (removalArmed)
        {
            foreach (var sibling in drag.Others) AnimateTranslate(sibling.Transform, 0, 0, drag.Animate);
            return;
        }

        var pointInRoot = e.GetPosition(_root);
        double clickPos = drag.Vertical ? pointInRoot.Y : pointInRoot.X;

        int target = 0;
        for (int i = 0; i < drag.Others.Count; i++)
        {
            var sibling = drag.Others[i];
            if (clickPos >= sibling.OriginalOffset + sibling.Size / 2) target = i + 1;
        }
        drag.CurrentTarget = target;

        for (int i = 0; i < drag.Others.Count; i++)
        {
            bool isBefore = i < drag.SourceIndex;
            int shift = isBefore ? (i >= target ? 1 : 0) : (i >= target ? 0 : -1);
            double offsetPx = shift * drag.SlotSize;
            AnimateTranslate(drag.Others[i].Transform, drag.Vertical ? 0 : offsetPx, drag.Vertical ? offsetPx : 0, drag.Animate);
        }
    }

    private void EndDrag(bool commit)
    {
        if (_drag is null) return;
        var drag = _drag;
        _drag = null;
        _escapeWatchTimer.Stop();
        drag.SourceElement.ReleaseMouseCapture();

        if (commit && drag.RemovalArmed)
        {
            drag.Ghost.Vanish(drag.Animate, () =>
            {
                drag.Ghost.Close();
                _cfg.Launchers.Remove(drag.Launcher);
                ConfigService.Save(_cfg);
                Rebuild();
            });
            return;
        }

        if (commit && drag.CurrentTarget != drag.SourceIndex)
        {
            drag.Ghost.FlyTo(ComputeTargetScreenCenter(drag), drag.Animate, () =>
            {
                drag.Ghost.Close();
                _cfg.Launchers.Remove(drag.Launcher);
                int insertAt = Math.Clamp(drag.CurrentTarget, 0, _cfg.Launchers.Count);
                _cfg.Launchers.Insert(insertAt, drag.Launcher);
                ConfigService.Save(_cfg);
                Rebuild();
            });
            return;
        }

        // Cancelado (Esc) ou soltou sem mudar de posição: volta tudo animado pro lugar
        // original, sem tocar no config.
        foreach (var sibling in drag.Others) AnimateTranslate(sibling.Transform, 0, 0, drag.Animate);
        drag.Ghost.FlyTo(drag.OriginalScreenCenter, drag.Animate, () =>
        {
            drag.Ghost.Close();
            drag.SourceElement.Opacity = 1;
        });
    }

    /// <summary>Centro de tela de onde o item vai parar se soltar agora — pro fantasma "voar" até lá antes do Rebuild().</summary>
    private Point ComputeTargetScreenCenter(DragState drag)
    {
        if (drag.Others.Count == 0) return drag.OriginalScreenCenter;

        double targetOffset = drag.CurrentTarget < drag.Others.Count
            ? drag.Others[drag.CurrentTarget].OriginalOffset
            : drag.Others[^1].OriginalOffset + drag.Others[^1].Size;
        targetOffset += drag.SlotSize / 2;

        var rootTopLeft = _root.PointToScreen(new Point(0, 0));
        return drag.Vertical
            ? new Point(rootTopLeft.X + _root.ActualWidth / 2, rootTopLeft.Y + targetOffset)
            : new Point(rootTopLeft.X + targetOffset, rootTopLeft.Y + _root.ActualHeight / 2);
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

    private System.Windows.Media.Imaging.BitmapSource? ResolveIconSource(LauncherConfig launcher)
    {
        string path = string.IsNullOrWhiteSpace(launcher.IconPath) ? launcher.Path : launcher.IconPath;
        return IconCacheService.GetOrExtract(path, _iconSize);
    }

    private static bool PathExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return true;
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return true;
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>Caminho de um .exe local pra comparar com processos em execução — nulo pra apps da Store/URLs/pastas, que não dá pra checar assim.</summary>
    private static string? TryGetLocalTargetPath(LauncherConfig launcher)
    {
        string path = launcher.Path;
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return null;

        string target = IconCacheService.ResolveTarget(path);
        return File.Exists(target) && string.Equals(System.IO.Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase)
            ? target
            : null;
    }

    private static void Launch(LauncherConfig launcher, bool forceAdmin)
    {
        if (!PathExists(launcher.Path)) return;

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = launcher.Path,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(launcher.Arguments)) info.Arguments = launcher.Arguments;
            if (!string.IsNullOrWhiteSpace(launcher.WorkingDirectory)) info.WorkingDirectory = launcher.WorkingDirectory;
            if (launcher.RunAsAdmin || forceAdmin) info.Verb = "runas";

            Process.Start(info);
        }
        catch
        {
            // Usuário cancelou o UAC, caminho ficou inválido entre o hover e o clique, etc.
        }
    }

    public void Dispose()
    {
        _runningTimer.Stop();
        _escapeWatchTimer.Stop();
    }
}
