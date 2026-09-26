using System.Globalization;
using Heimdall.Config;

namespace Heimdall.Widgets;

/// <summary>Gera o texto do relógio a partir de Mode/Style/Culture — usado pelo ClockWidget e pelo preview da tela de Configurações.</summary>
public static class ClockFormatter
{
    private sealed record StyleFormats(string Time, string Date, string VerticalDate, string Separator);

    private static readonly Dictionary<ClockStyle, StyleFormats> Styles = new()
    {
        [ClockStyle.Classic] = new("HH:mm", "ddd, dd/MM/yyyy", "dd/MM", "   "),
        [ClockStyle.Compact] = new("HH:mm", "dd/MM", "dd/MM", " "),
        [ClockStyle.Verbose] = new("HH:mm:ss", "dddd, dd 'de' MMMM 'de' yyyy", "dd/MM", "   "),
        [ClockStyle.ISO] = new("HH:mm:ss", "yyyy-MM-dd", "MM-dd", " "),
    };

    public static string Format(ClockConfig cfg, DateTime now, bool vertical)
    {
        var culture = ResolveCulture(cfg.Culture);

        if (cfg.Mode == ClockMode.Custom)
            return FormatPart(now, cfg.CustomFormat, culture);

        var s = Styles[cfg.Style];
        return cfg.Mode switch
        {
            ClockMode.TimeOnly => FormatPart(now, s.Time, culture),
            ClockMode.DateOnly => FormatPart(now, vertical ? s.VerticalDate : s.Date, culture),
            _ => vertical
                ? Join("\n", FormatPart(now, s.Time, culture), FormatPart(now, s.VerticalDate, culture))
                : Join(s.Separator, FormatPart(now, s.Date, culture), FormatPart(now, s.Time, culture))
        };
    }

    public static CultureInfo ResolveCulture(string? name)
    {
        try { return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(name) ? "pt-BR" : name); }
        catch (CultureNotFoundException) { return CultureInfo.CurrentCulture; }
    }

    private static string FormatPart(DateTime value, string? format, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(format)) return string.Empty;
        try { return value.ToString(format, culture); }
        catch (FormatException) { return "?"; }
    }

    private static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(p => p.Length > 0));
}
