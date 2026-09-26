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
using Heimdall.Services;
using Heimdall.UI;

namespace Heimdall.Widgets;

/// <summary>Ícones de atalhos (executáveis, .lnk, pastas, arquivos, URLs, apps da Store) lado a lado ou empilhados.</summary>
public sealed class LauncherWidget : IWidget
{
    private const int IconMargin = 6;
    private const int MinIconSize = 16;
    private const int MaxIconSize = 48;

    private readonly AppConfig _cfg;
    private readonly EffectiveStyle _style;
    private readonly int _iconSize;
    private readonly StackPanel _root = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _runningTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly List<(string TargetPath, Ellipse Dot)> _runningIndicators = new();

    public FrameworkElement View => _root;

    public LauncherWidget(AppConfig cfg)
    {
        _cfg = cfg;
        _style = ThemeService.GetEffectiveStyle(cfg);
        _iconSize = Math.Clamp(cfg.Thickness - IconMargin * 2, MinIconSize, MaxIconSize);
        _runningTimer.Tick += (_, _) => CheckRunning();
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

    /// <summary>Compara com os processos em execução a cada poucos segundos — só pra atalhos locais (exe/.lnk), não dá pra checar apps da Store/URLs assim.</summary>
    private void CheckRunning()
    {
        if (_runningIndicators.Count == 0) return;

        var runningPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainModule?.FileName is { } path) runningPaths.Add(path);
            }
            catch
            {
                // Processo elevado/do sistema sem permissão de leitura — ignora e segue.
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var (targetPath, dot) in _runningIndicators)
            dot.Visibility = runningPaths.Contains(targetPath) ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateIcon(LauncherConfig launcher)
    {
        bool exists = PathExists(launcher.Path);
        var iconSource = exists ? ResolveIconSource(launcher) : null;

        // Sem ícone extraído (caminho quebrado, ou falha na extração) — glifo genérico no
        // lugar da imagem em branco, senão o atalho "quebrado" fica invisível na barra.
        FrameworkElement content;
        if (iconSource is not null)
        {
            content = new Image { Width = _iconSize, Height = _iconSize, Stretch = Stretch.Uniform, Source = iconSource };
        }
        else
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
            content = glyph;
        }

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
            _runningIndicators.Add((targetPath, dot));
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
            ToolTip = exists ? launcher.Name : $"{launcher.Name} — Atalho não encontrado"
        };

        border.MouseEnter += (_, _) =>
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(_style.Hover, TimeSpan.FromMilliseconds(150)));
        border.MouseLeave += (_, _) =>
            background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(150)));

        border.MouseLeftButtonUp += (_, _) => Launch(launcher, forceAdmin: false);

        SetupDrag(border, launcher);
        border.ContextMenu = BuildContextMenu(launcher, border);

        return border;
    }

    // ---------- Separadores dentro da lista de atalhos ----------

    private Border CreateSeparatorItem(LauncherConfig separator)
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

        SetupDrag(container, separator);
        container.ContextMenu = BuildSeparatorContextMenu(separator);

        return container;
    }

    private ContextMenu BuildSeparatorContextMenu(LauncherConfig separator)
    {
        var line = new MenuItem { Header = "Linha", IsCheckable = true, IsChecked = separator.Style == SeparatorStyle.Line };
        var space = new MenuItem { Header = "Espaço", IsCheckable = true, IsChecked = separator.Style == SeparatorStyle.Space };
        var dot = new MenuItem { Header = "Ponto", IsCheckable = true, IsChecked = separator.Style == SeparatorStyle.Dot };
        line.Click += (_, _) => ChangeSeparatorStyle(separator, SeparatorStyle.Line);
        space.Click += (_, _) => ChangeSeparatorStyle(separator, SeparatorStyle.Space);
        dot.Click += (_, _) => ChangeSeparatorStyle(separator, SeparatorStyle.Dot);

        var remove = new MenuItem { Header = "Remover" };
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
        var runAsAdmin = new MenuItem { Header = "Executar como administrador" };
        runAsAdmin.Click += (_, _) => Launch(launcher, forceAdmin: true);

        var openLocation = new MenuItem { Header = "Abrir local do arquivo" };
        openLocation.Click += (_, _) => OpenFileLocation(launcher);

        var rename = new MenuItem { Header = "Renomear..." };
        rename.Click += (_, _) => RenameLauncher(launcher, icon);

        var remove = new MenuItem { Header = "Remover" };
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

    // ---------- Arrastar pra reordenar ----------

    private Point? _dragStart;
    private LauncherConfig? _dragSource;

    private void SetupDrag(Border icon, LauncherConfig launcher)
    {
        // _dragStart/_dragSource são campos únicos do widget (não por ícone) — sem checar
        // _dragSource == launcher, se o cursor passasse por cima de OUTRO ícone antes de
        // cruzar o limiar de arrasto, era o ícone errado (o que está por baixo do cursor
        // agora, não o que recebeu o MouseDown) que iniciava o DoDragDrop.
        icon.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(null);
            _dragSource = launcher;
            icon.CaptureMouse();
        };

        icon.PreviewMouseMove += (_, e) =>
        {
            if (_dragStart is null || _dragSource != launcher || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            _dragStart = null;
            _dragSource = null;
            icon.ReleaseMouseCapture();
            DragDrop.DoDragDrop(icon, launcher, DragDropEffects.Move);
        };

        icon.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (_dragSource == launcher) { _dragStart = null; _dragSource = null; }
            icon.ReleaseMouseCapture();
        };

        icon.AllowDrop = true;
        icon.Drop += (_, e) =>
        {
            if (e.Data.GetData(typeof(LauncherConfig)) is not LauncherConfig dragged || dragged == launcher) return;

            int oldIndex = _cfg.Launchers.IndexOf(dragged);
            int newIndex = _cfg.Launchers.IndexOf(launcher);
            if (oldIndex < 0 || newIndex < 0) return;

            _cfg.Launchers.RemoveAt(oldIndex);
            _cfg.Launchers.Insert(newIndex, dragged);
            ConfigService.Save(_cfg);
            Rebuild();
        };
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

    public void Dispose() => _runningTimer.Stop();
}
