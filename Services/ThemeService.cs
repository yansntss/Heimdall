using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Native;
using Microsoft.Win32;

namespace Heimdall.Services;

/// <summary>
/// Cores do tema ativo já resolvidas, pra UI montada em código (popups, janelas auxiliares,
/// widgets antigos). Background é a superfície de popup (#12161E @ 94%), não a da barra —
/// a barra usa {DynamicResource H.Brush.BarBg} direto.
/// </summary>
internal sealed record EffectiveStyle(
    Color Background, Color Foreground, Color Accent, Color Hover, Color Border,
    FontFamily FontFamily, double FontSize, double CornerRadius, bool Acrylic);

/// <summary>
/// Liga o config ao <see cref="ThemeManager"/>: escolhe o tema (embutido ou themes/*.json),
/// resolve o acento ("system" = cor de destaque do Windows) e os overrides de BarStyle.
/// </summary>
internal static class ThemeService
{
    public const string SystemAccent = "system";

    /// <summary>Altura fixa da variante Compacta (barra horizontal).</summary>
    public const int CompactThickness = 26;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ThemesDir => Path.Combine(ConfigService.ConfigDir, "themes");

    public static List<ThemeDefinition> LoadUserThemes()
    {
        var result = new List<ThemeDefinition>();
        if (!Directory.Exists(ThemesDir)) return result;

        foreach (var file in Directory.EnumerateFiles(ThemesDir, "*.json"))
        {
            try
            {
                var theme = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(file), JsonOptions);
                if (theme is not null && !string.IsNullOrWhiteSpace(theme.Name)) result.Add(theme);
            }
            catch { /* ignora arquivo de tema inválido */ }
        }
        return result;
    }

    /// <summary>Ids embutidos + nomes dos temas JSON do usuário.</summary>
    public static List<string> GetAllThemeNames() =>
        ThemeManager.BuiltInThemes
            .Concat(LoadUserThemes().Select(t => t.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Acento acompanha o Windows — muda de cor quando o usuário troca o destaque do sistema.</summary>
    public static bool FollowsSystem(AppConfig cfg) =>
        string.Equals(cfg.AccentColor, SystemAccent, StringComparison.OrdinalIgnoreCase);

    /// <summary>Aplica o tema do config no app inteiro (pincéis H.Brush.* ao vivo).</summary>
    public static void ApplyToApp(AppConfig cfg)
    {
        var (theme, overrides) = Resolve(cfg);
        ThemeManager.Apply(theme, overrides);
    }

    /// <summary>Paleta que <see cref="ApplyToApp"/> aplicaria — sem aplicar (cards de prévia).</summary>
    public static ThemePalette Preview(AppConfig cfg, string themeName)
    {
        var original = cfg.Theme;
        cfg.Theme = themeName;
        try
        {
            var (theme, overrides) = Resolve(cfg);
            return ThemeManager.Preview(theme, overrides);
        }
        finally { cfg.Theme = original; }
    }

    private static (string Theme, ThemeOverrides Overrides) Resolve(AppConfig cfg)
    {
        var user = ThemeManager.IsBuiltIn(cfg.Theme)
            ? null
            : LoadUserThemes().FirstOrDefault(t => string.Equals(t.Name, cfg.Theme, StringComparison.OrdinalIgnoreCase));

        // Tema JSON do usuário: parte da Obsidiana (ou Cristal, se o fundo for claro) e
        // troca as cores que o arquivo define.
        string baseTheme = user is null
            ? cfg.Theme
            : ParseColor(user.Background) is { } bg && Luminance(bg) > 0.6 ? "Cristal" : ThemeManager.DefaultTheme;

        var overrides = new ThemeOverrides(
            Accent: ResolveAccent(cfg) ?? ParseColor(user?.Accent),
            BarOpacity: cfg.BarOpacity,
            BarBackground: ParseColor(cfg.Style.Background) ?? ParseColor(user?.Background),
            Text: ParseColor(cfg.Style.Foreground) ?? ParseColor(user?.TextPrimary),
            TextSecondary: ParseColor(user?.TextSecondary),
            Line: ParseColor(user?.Border),
            Overlay: ParseColor(user?.Hover) is { } hover ? Color.FromRgb(hover.R, hover.G, hover.B) : null,
            Acrylic: user is null ? null : user.Backdrop is BackdropType.Acrylic or BackdropType.Mica or BackdropType.Translucent);

        return (baseTheme, overrides);
    }

    /// <summary>Null = acento do tema.</summary>
    public static Color? ResolveAccent(AppConfig cfg) =>
        FollowsSystem(cfg) ? GetSystemAccent() : ParseColor(cfg.AccentColor);

    /// <summary>Altura (horizontal) ou largura (vertical) efetiva — a Compacta fixa 26 na horizontal.</summary>
    public static int BarThickness(AppConfig cfg)
    {
        bool vertical = cfg.Edge is BarEdge.Left or BarEdge.Right;
        if (!vertical && ThemeManager.Palette.IsCompact) return CompactThickness;
        return Math.Clamp(cfg.Thickness, 16, 400);
    }

    public static EffectiveStyle GetEffectiveStyle(AppConfig cfg)
    {
        var p = ThemeManager.Palette;
        var font = string.IsNullOrWhiteSpace(cfg.Style.FontFamily)
            ? (FontFamily)System.Windows.Application.Current.FindResource("H.Font.UI")
            : new FontFamily(cfg.Style.FontFamily);

        return new EffectiveStyle(
            Background: ThemeManager.WithOpacity(p.Surface, 0.94),
            Foreground: p.Text,
            Accent: p.Accent,
            Hover: ThemeManager.WithOpacity(p.Overlay, 0.07),
            Border: p.Line,
            FontFamily: font,
            FontSize: Math.Clamp(cfg.Style.FontSize ?? 12, 8, 72),
            CornerRadius: 12,
            Acrylic: p.Acrylic);
    }

    /// <summary>
    /// Cores de um tema pelo nome no formato antigo — ainda usado pelos cards de prévia da
    /// tela de Configurações atual (refeita na fase 3).
    /// </summary>
    public static ThemeDefinition GetThemeDefinition(string name)
    {
        var p = ThemeManager.Preview(name);
        return new ThemeDefinition
        {
            Name = name,
            Background = Hex(ThemeManager.WithOpacity(p.BarBackground, p.BarOpacity)),
            TextPrimary = Hex(p.Text),
            TextSecondary = Hex(p.TextSecondary),
            Accent = Hex(p.Accent),
            Hover = Hex(ThemeManager.WithOpacity(p.Overlay, 0.07)),
            Border = Hex(p.Line),
            CornerRadius = 8,
            Backdrop = p.Acrylic ? BackdropType.Acrylic : BackdropType.Solid,
            FontFamily = "Manrope"
        };
    }

    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch { return false; }
    }

    /// <summary>Cor de destaque do Windows (opaca); null se o DWM não responder.</summary>
    public static Color? GetSystemAccent()
    {
        try
        {
            if (NativeMethods.DwmGetColorizationColor(out uint color, out _) != 0) return null;
            return Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color);
        }
        catch { return null; }
    }

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return ColorConverter.ConvertFromString(hex) as Color?; }
        catch (FormatException) { return null; }
    }

    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
}
