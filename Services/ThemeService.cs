using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Native;
using Microsoft.Win32;

namespace Heimdall.Services;

/// <summary>Resultado final de mesclar o tema selecionado com os overrides de <see cref="BarStyle"/>.</summary>
internal sealed record EffectiveStyle(
    Color Background, Color Foreground, Color Accent, Color Hover, Color Border,
    string FontFamily, double FontSize, double CornerRadius, BackdropType Backdrop);

internal static class ThemeService
{
    private const string AutoName = "Auto";
    private const string AccentName = "Destaque do Windows";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Escuro é o default histórico da Fase 1 — preservado como está pra não quebrar
    // a aparência de configs antigos que não têm "Theme" no JSON.
    private static readonly List<ThemeDefinition> BuiltIn = new()
    {
        new() { Name = "Escuro", Background = "#E61E1E1E", TextPrimary = "#FFFFFFFF", TextSecondary = "#FFB0B0B0",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 0, Backdrop = BackdropType.Solid },
        new() { Name = "Claro", Background = "#E6F5F5F5", TextPrimary = "#FF1E1E1E", TextSecondary = "#FF5A5A5A",
            Accent = "#FF0067C0", Hover = "#22000000", Border = "#22000000", CornerRadius = 0, Backdrop = BackdropType.Solid },
        new() { Name = "Translúcido Escuro", Background = "#991E1E1E", TextPrimary = "#FFFFFFFF", TextSecondary = "#FFB0B0B0",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 8, Backdrop = BackdropType.Translucent },
        new() { Name = "Translúcido Claro", Background = "#99F5F5F5", TextPrimary = "#FF1E1E1E", TextSecondary = "#FF5A5A5A",
            Accent = "#FF0067C0", Hover = "#22000000", Border = "#22000000", CornerRadius = 8, Backdrop = BackdropType.Translucent },
        new() { Name = "Acrílico", Background = "#401E1E1E", TextPrimary = "#FFFFFFFF", TextSecondary = "#FFB0B0B0",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 8, Backdrop = BackdropType.Acrylic },
        new() { Name = "Mica", Background = "#101E1E1E", TextPrimary = "#FFFFFFFF", TextSecondary = "#FFB0B0B0",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 8, Backdrop = BackdropType.Mica },
        new() { Name = "Vidro", Background = "#05FFFFFF", TextPrimary = "#FFFFFFFF", TextSecondary = "#CCFFFFFF",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 8, Backdrop = BackdropType.None },
        new() { Name = AccentName, Background = "#E61E1E1E", TextPrimary = "#FFFFFFFF", TextSecondary = "#FFB0B0B0",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 0, Backdrop = BackdropType.Solid },
        new() { Name = AutoName, Background = "#E61E1E1E", TextPrimary = "#FFFFFFFF", TextSecondary = "#FFB0B0B0",
            Accent = "#FF4A9EFF", Hover = "#22FFFFFF", Border = "#22FFFFFF", CornerRadius = 0, Backdrop = BackdropType.Solid },
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

    public static bool IsLiveTheme(string name) =>
        string.Equals(name, AutoName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, AccentName, StringComparison.OrdinalIgnoreCase);

    public static List<string> GetAllThemeNames() =>
        BuiltIn.Select(t => t.Name)
            .Concat(LoadUserThemes().Select(t => t.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static EffectiveStyle GetEffectiveStyle(AppConfig cfg)
    {
        var theme = ResolveBase(cfg.Theme);
        var style = cfg.Style;

        return new EffectiveStyle(
            Background: ParseColor(style.Background) ?? ParseColor(theme.Background) ?? Colors.Black,
            Foreground: ParseColor(style.Foreground) ?? ParseColor(theme.TextPrimary) ?? Colors.White,
            Accent: ParseColor(theme.Accent) ?? Colors.DodgerBlue,
            Hover: ParseColor(theme.Hover) ?? Colors.Transparent,
            Border: ParseColor(theme.Border) ?? Colors.Transparent,
            FontFamily: string.IsNullOrWhiteSpace(style.FontFamily) ? theme.FontFamily : style.FontFamily,
            FontSize: Math.Clamp(style.FontSize ?? 13, 8, 72),
            CornerRadius: theme.CornerRadius,
            Backdrop: theme.Backdrop);
    }

    private static ThemeDefinition ResolveBase(string name)
    {
        if (string.Equals(name, AutoName, StringComparison.OrdinalIgnoreCase))
            return BuiltIn.First(t => t.Name == (IsSystemLightTheme() ? "Claro" : "Escuro"));

        var builtin = BuiltIn.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (builtin is not null)
        {
            if (string.Equals(name, AccentName, StringComparison.OrdinalIgnoreCase) && GetAccentColorHex() is { } accent)
                return WithAccent(builtin, accent);
            return builtin;
        }

        return LoadUserThemes().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? BuiltIn[0];
    }

    private static ThemeDefinition WithAccent(ThemeDefinition source, string accentHex) => new()
    {
        Name = source.Name,
        Background = source.Background,
        TextPrimary = source.TextPrimary,
        TextSecondary = source.TextSecondary,
        Accent = accentHex,
        Hover = source.Hover,
        Border = source.Border,
        CornerRadius = source.CornerRadius,
        Backdrop = source.Backdrop,
        FontFamily = source.FontFamily
    };

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch { return false; }
    }

    private static string? GetAccentColorHex()
    {
        try
        {
            return NativeMethods.DwmGetColorizationColor(out uint color, out _) == 0
                ? $"#{color:X8}"
                : null;
        }
        catch { return null; }
    }

    private static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return ColorConverter.ConvertFromString(hex) as Color?; }
        catch (FormatException) { return null; }
    }
}
