using System.Windows;
using System.Windows.Media;

namespace Heimdall.Services;

/// <summary>Cores e métricas resolvidas do tema ativo, pra quem monta UI em código.</summary>
internal sealed record ThemePalette(
    Color BarBackground, double BarOpacity, bool Acrylic, double AccentLine, double BarHeight, bool IsCompact, bool IsLight,
    Color Surface, Color Elevated, Color Line, Color Text, Color TextSecondary, Color TextTertiary,
    Color Accent, Color OnAccent, Color Ok, Color Danger, Color Overlay, Color Muted);

/// <summary>
/// Trocas por cima do tema escolhido — acento e opacidade (tela de Aparência), cores
/// soltas de BarStyle e temas próprios em themes/*.json. Nulo = valor do tema.
/// </summary>
internal sealed record ThemeOverrides(
    Color? Accent = null,
    double? BarOpacity = null,
    Color? BarBackground = null,
    Color? Text = null,
    Color? TextSecondary = null,
    Color? Line = null,
    Color? Overlay = null,
    bool? Acrylic = null);

/// <summary>
/// Dono dos ResourceDictionaries de tema no Application.Resources. Cada tema
/// (Themes/&lt;Nome&gt;.xaml) traz só cores e métricas; os pincéis "H.Brush.*" são gerados
/// aqui, já com a cor de acento e a opacidade da barra escolhidas pelo usuário — por isso
/// os dois são trocáveis em runtime sem um dicionário por combinação. Telas usam
/// {DynamicResource H.Brush.*} e acompanham a troca na hora.
/// </summary>
internal static class ThemeManager
{
    public const string DefaultTheme = "Obsidiana";

    /// <summary>Ids dos temas embutidos (nome do arquivo em Themes/), na ordem dos cards.</summary>
    public static readonly IReadOnlyList<string> BuiltInThemes = new[] { "Obsidiana", "Cristal", "Bifrost", "Compacta" };

    public static readonly Color DefaultAccent = Color.FromRgb(0xE0, 0xB5, 0x62);

    private static ResourceDictionary? _themeDictionary;
    private static ResourceDictionary? _brushDictionary;

    public static ThemePalette Palette { get; private set; } = null!;

    public static string CurrentTheme { get; private set; } = DefaultTheme;

    /// <summary>Disparado depois de cada <see cref="Apply"/> — pra quem precisa reagir em código (backdrop DWM, sparkline etc.).</summary>
    public static event Action? Changed;

    public static string DisplayName(string theme) => theme == "Bifrost" ? "Bifröst" : theme;

    public static bool IsBuiltIn(string? theme) =>
        BuiltInThemes.Any(t => string.Equals(t, theme, StringComparison.OrdinalIgnoreCase));

    public static void Initialize()
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Add(Load("Tokens"));
        merged.Add(Load("Icons"));
        _themeDictionary = Load(DefaultTheme);
        merged.Add(_themeDictionary);
        _brushDictionary = new ResourceDictionary();
        merged.Add(_brushDictionary);
        merged.Add(Load("Controls"));
        merged.Add(Load("Menus"));

        Apply(DefaultTheme);
    }

    /// <param name="theme">Id de um tema embutido; desconhecido cai no padrão.</param>
    /// <param name="overrides">Acento, opacidade e cores trocadas por cima do tema (null = tema puro).</param>
    public static void Apply(string? theme, ThemeOverrides? overrides = null)
    {
        if (_themeDictionary is null || _brushDictionary is null)
            throw new InvalidOperationException("ThemeManager.Initialize() não foi chamado.");

        string id = ResolveId(theme);
        var merged = Application.Current.Resources.MergedDictionaries;

        if (id != CurrentTheme)
        {
            var next = Load(id);
            merged[merged.IndexOf(_themeDictionary)] = next;
            _themeDictionary = next;
            CurrentTheme = id;
        }

        Palette = Resolve(_themeDictionary, overrides);

        var brushes = BuildBrushes(Palette);
        merged[merged.IndexOf(_brushDictionary)] = brushes;
        _brushDictionary = brushes;

        Changed?.Invoke();
    }

    /// <summary>Paleta de um tema sem aplicá-lo — prévia dos cards de tema.</summary>
    public static ThemePalette Preview(string? theme, ThemeOverrides? overrides = null) =>
        Resolve(ResolveId(theme) == CurrentTheme && _themeDictionary is not null ? _themeDictionary : Load(ResolveId(theme)), overrides);

    private static string ResolveId(string? theme) =>
        BuiltInThemes.FirstOrDefault(t => string.Equals(t, theme, StringComparison.OrdinalIgnoreCase)) ?? DefaultTheme;

    private static ThemePalette Resolve(ResourceDictionary d, ThemeOverrides? o)
    {
        bool acrylic = o?.Acrylic ?? (string)d["H.Theme.Backdrop"] == "Acrylic";
        double opacity = acrylic ? Math.Clamp(o?.BarOpacity ?? (double)d["H.Theme.BarOpacity"], 0.2, 1) : 1;

        // Cor de fundo própria (BarStyle/tema JSON) traz o alfa dela; a opacidade vira
        // multiplicador só quando foi escolhida explicitamente.
        var barBackground = o?.BarBackground ?? (Color)d["H.Color.BarBg"];
        if (o?.BarBackground is { } custom)
        {
            barBackground = Color.FromRgb(custom.R, custom.G, custom.B);
            if (o.BarOpacity is null) opacity = custom.A / 255.0;
        }

        return new ThemePalette(
            BarBackground: barBackground,
            BarOpacity: opacity,
            Acrylic: acrylic,
            AccentLine: (double)d["H.Theme.AccentLine"],
            BarHeight: (double)d["H.Size.BarHeight"],
            IsCompact: (bool)d["H.Theme.IsCompact"],
            IsLight: (bool)d["H.Theme.IsLight"],
            Surface: (Color)d["H.Color.Surface"],
            Elevated: (Color)d["H.Color.Elevated"],
            Line: o?.Line ?? (Color)d["H.Color.Line"],
            Text: o?.Text ?? (Color)d["H.Color.Text"],
            TextSecondary: o?.TextSecondary ?? (Color)d["H.Color.TextSecondary"],
            TextTertiary: (Color)d["H.Color.TextTertiary"],
            Accent: o?.Accent ?? (Color)d["H.Color.Accent"],
            OnAccent: (Color)d["H.Color.OnAccent"],
            Ok: (Color)d["H.Color.Ok"],
            Danger: (Color)d["H.Color.Danger"],
            Overlay: o?.Overlay ?? (Color)d["H.Color.Overlay"],
            Muted: (Color)d["H.Color.Muted"]);
    }

    private static ResourceDictionary BuildBrushes(ThemePalette p)
    {
        var d = new ResourceDictionary
        {
            // Acento sobrescreve o do tema (este dicionário vem depois no MergedDictionaries).
            ["H.Color.Accent"] = p.Accent,

            ["H.Brush.BarBg"] = Brush(p.BarBackground, p.BarOpacity),
            ["H.Brush.Surface"] = Brush(p.Surface),
            ["H.Brush.SurfacePopup"] = Brush(p.Surface, 0.94),
            ["H.Brush.Elevated"] = Brush(p.Elevated, 0.98),
            ["H.Brush.Line"] = Brush(p.Line),
            ["H.Brush.Text"] = Brush(p.Text),
            ["H.Brush.TextSecondary"] = Brush(p.TextSecondary),
            ["H.Brush.TextTertiary"] = Brush(p.TextTertiary),
            ["H.Brush.Accent"] = Brush(p.Accent),
            ["H.Brush.AccentWash"] = Brush(p.Accent, 0.14),
            ["H.Brush.AccentSoft"] = Brush(p.Accent, 0.28),
            ["H.Brush.OnAccent"] = Brush(p.OnAccent),
            ["H.Brush.Ok"] = Brush(p.Ok),
            ["H.Brush.Danger"] = Brush(p.Danger),
            ["H.Brush.Muted"] = Brush(p.Muted),
            ["H.Brush.Hover"] = Brush(p.Overlay, 0.07),
            ["H.Brush.Pressed"] = Brush(p.Overlay, 0.12),
            ["H.Brush.Selected"] = Brush(p.Overlay, 0.08),
            ["H.Brush.Track"] = Brush(p.Overlay, 0.12),
            // Camadas da janela de Configurações: sidebar, cards e campos.
            ["H.Brush.Sidebar"] = Brush(p.Overlay, 0.03),
            ["H.Brush.Card"] = Brush(p.Overlay, 0.04),
            ["H.Brush.Field"] = Brush(p.Overlay, 0.06),
        };
        return d;
    }

    /// <summary>Opacidade multiplica o alfa que a cor já tiver (ex.: borda #FFFFFF @ 9%).</summary>
    public static SolidColorBrush Brush(Color color, double opacity = 1)
    {
        var brush = new SolidColorBrush(WithOpacity(color, opacity));
        brush.Freeze();
        return brush;
    }

    public static Color WithOpacity(Color color, double opacity)
    {
        var c = color;
        c.A = (byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1));
        return c;
    }

    private static ResourceDictionary Load(string name) =>
        new() { Source = new Uri($"pack://application:,,,/Heimdall;component/Themes/{name}.xaml", UriKind.Absolute) };
}
