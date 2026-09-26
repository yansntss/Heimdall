using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Heimdall.Config;
using Heimdall.Services;

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

    public FrameworkElement View => _root;

    public LauncherWidget(AppConfig cfg)
    {
        _cfg = cfg;
        _style = ThemeService.GetEffectiveStyle(cfg);
        _iconSize = Math.Clamp(cfg.Thickness - IconMargin * 2, MinIconSize, MaxIconSize);
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start() => Rebuild();

    private void Rebuild()
    {
        _root.Children.Clear();
        foreach (var launcher in _cfg.Launchers)
            _root.Children.Add(CreateIcon(launcher));
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

        border.MouseLeftButtonUp += (_, _) => Launch(launcher);

        return border;
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

    private static void Launch(LauncherConfig launcher)
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
            if (launcher.RunAsAdmin) info.Verb = "runas";

            Process.Start(info);
        }
        catch
        {
            // Usuário cancelou o UAC, caminho ficou inválido entre o hover e o clique, etc.
        }
    }

    public void Dispose() { }
}
