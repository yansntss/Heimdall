using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.ViewModels;

/// <summary>Card de posição da barra, com o mini-diagrama da borda ativa.</summary>
internal sealed class EdgeOption
{
    public EdgeOption(BarEdge edge)
    {
        Edge = edge;
        Name = Strings.EdgeName(edge);
        bool vertical = edge is BarEdge.Left or BarEdge.Right;
        BarWidth = vertical ? 6 : double.NaN;
        BarHeight = vertical ? double.NaN : 6;
        BarHorizontal = edge switch { BarEdge.Left => HorizontalAlignment.Left, BarEdge.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Stretch };
        BarVertical = edge switch { BarEdge.Top => VerticalAlignment.Top, BarEdge.Bottom => VerticalAlignment.Bottom, _ => VerticalAlignment.Stretch };
    }

    public BarEdge Edge { get; }
    public string Name { get; }
    public double BarWidth { get; }
    public double BarHeight { get; }
    public HorizontalAlignment BarHorizontal { get; }
    public VerticalAlignment BarVertical { get; }
}

/// <summary>Card de tema com a prévia da barra nas cores daquele tema (e do acento escolhido).</summary>
internal sealed class ThemeOption : ObservableObject
{
    private Brush _bar = Brushes.Transparent, _text = Brushes.White, _secondary = Brushes.Gray, _accent = Brushes.Gold, _chip = Brushes.Transparent;
    private double _accentLine, _barHeight = 22;

    public ThemeOption(string id) => Id = id;

    public string Id { get; }
    public string Name => ThemeManager.DisplayName(Id);
    public Brush Bar { get => _bar; private set => SetProperty(ref _bar, value); }
    public Brush Text { get => _text; private set => SetProperty(ref _text, value); }
    public Brush Secondary { get => _secondary; private set => SetProperty(ref _secondary, value); }
    public Brush Accent { get => _accent; private set => SetProperty(ref _accent, value); }
    public Brush Chip { get => _chip; private set => SetProperty(ref _chip, value); }
    public double AccentLine { get => _accentLine; private set => SetProperty(ref _accentLine, value); }
    public double BarHeight { get => _barHeight; private set => SetProperty(ref _barHeight, value); }

    public void Refresh(ThemePalette p)
    {
        Bar = ThemeManager.Brush(p.BarBackground, p.BarOpacity);
        Text = ThemeManager.Brush(p.Text);
        Secondary = ThemeManager.Brush(p.TextSecondary);
        Accent = ThemeManager.Brush(p.Accent);
        Chip = ThemeManager.Brush(p.Overlay, 0.10);
        AccentLine = p.AccentLine;
        BarHeight = p.IsCompact ? 16 : 22;
    }
}

/// <summary>Swatch de cor de acento.</summary>
internal sealed class AccentOption
{
    public AccentOption(string key, string hex)
    {
        Key = key;
        Hex = hex;
        Name = Strings.AccentName(key);
        Brush = ThemeManager.Brush((Color)ColorConverter.ConvertFromString(hex));
    }

    public string Key { get; }
    public string Hex { get; }
    public string Name { get; }
    public Brush Brush { get; }
}

/// <summary>
/// Painel Aparência. Escreve direto no rascunho do config (<see cref="AppConfig"/> da
/// janela) e pede prévia ao vivo a cada mudança: cor/opacidade só repinta, o resto recria
/// a barra. Nada vai pro disco até o Aplicar.
/// </summary>
internal sealed class AppearanceViewModel : ObservableObject
{
    private const int ThicknessStep = 2;

    private readonly AppConfig _draft;
    private readonly Action<bool> _preview;

    public AppearanceViewModel(AppConfig draft, Action<bool> preview)
    {
        _draft = draft;
        _preview = preview;

        Edges = Enum.GetValues<BarEdge>().Select(e => new EdgeOption(e)).ToList();
        Themes = ThemeManager.BuiltInThemes.Select(id => new ThemeOption(id)).ToList();
        Accents = new List<AccentOption>
        {
            new("gold", "#E0B562"), new("teal", "#8FD3C8"), new("blue", "#7FA6F2"), new("violet", "#B59CF5")
        };

        DecreaseThicknessCommand = new RelayCommand(() => Thickness -= ThicknessStep, () => IsThicknessEditable);
        IncreaseThicknessCommand = new RelayCommand(() => Thickness += ThicknessStep, () => IsThicknessEditable);
        UseWindowsAccentCommand = new RelayCommand(() => UseWindowsAccent = true);

        RefreshThemeCards();
    }

    public IReadOnlyList<EdgeOption> Edges { get; }
    public IReadOnlyList<ThemeOption> Themes { get; }
    public IReadOnlyList<AccentOption> Accents { get; }

    public ICommand DecreaseThicknessCommand { get; }
    public ICommand IncreaseThicknessCommand { get; }
    public ICommand UseWindowsAccentCommand { get; }

    private bool IsVertical => _draft.Edge is BarEdge.Left or BarEdge.Right;

    public EdgeOption? SelectedEdge
    {
        get => Edges.FirstOrDefault(e => e.Edge == _draft.Edge);
        set
        {
            if (value is null || value.Edge == _draft.Edge) return;
            bool wasVertical = IsVertical;
            _draft.Edge = value.Edge;
            // Trocar entre horizontal e vertical: a espessura muda de significado (altura ↔
            // largura) — volta pro padrão de cada orientação em vez de uma barra de 38px de largura.
            if (wasVertical != IsVertical) _draft.Thickness = IsVertical ? 72 : 38;
            Changed(rebuild: true, nameof(SelectedEdge), nameof(Thickness), nameof(ThicknessLabel));
        }
    }

    public ThemeOption? SelectedTheme
    {
        get => Themes.FirstOrDefault(t => string.Equals(t.Id, _draft.Theme, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || value.Id == _draft.Theme) return;
            _draft.Theme = value.Id;
            Changed(rebuild: true, nameof(SelectedTheme), nameof(OpacityPercent), nameof(IsOpacityEnabled),
                nameof(IsThicknessEditable), nameof(ThicknessHint));
        }
    }

    private ThemePalette CurrentPalette => ThemeService.Preview(_draft, _draft.Theme);

    public bool IsOpacityEnabled => CurrentPalette.Acrylic;

    /// <summary>20–100. Sem valor salvo, mostra o do tema (76% na Obsidiana).</summary>
    public double OpacityPercent
    {
        get => Math.Round((_draft.BarOpacity ?? CurrentPalette.BarOpacity) * 100);
        set
        {
            double clamped = Math.Clamp(Math.Round(value), 20, 100);
            if (_draft.BarOpacity is { } current && Math.Abs(current * 100 - clamped) < 0.5) return;
            _draft.BarOpacity = clamped / 100;
            RefreshThemeCards();
            Changed(rebuild: false, nameof(OpacityPercent));
        }
    }

    public string ThicknessLabel => IsVertical ? Strings.LabelBarWidth : Strings.LabelBarHeight;

    /// <summary>Compacta fixa 26px na horizontal — o stepper não teria efeito.</summary>
    public bool IsThicknessEditable => IsVertical || !ThemeManager.Preview(_draft.Theme).IsCompact;

    public string? ThicknessHint => IsThicknessEditable ? null : Strings.HintHeightCompact;

    public int Thickness
    {
        get => _draft.Thickness;
        set
        {
            int clamped = IsVertical ? Math.Clamp(value, 40, 400) : Math.Clamp(value, 24, 64);
            if (clamped == _draft.Thickness) { OnPropertyChanged(); return; }
            _draft.Thickness = clamped;
            Changed(rebuild: true, nameof(Thickness));
        }
    }

    public AccentOption? SelectedAccent
    {
        get => Accents.FirstOrDefault(a => string.Equals(a.Hex, _draft.AccentColor, StringComparison.OrdinalIgnoreCase))
               ?? (_draft.AccentColor is null ? Accents[0] : null);
        set
        {
            if (value is null) return;
            // O ouro é o acento do próprio tema: salva como "sem override".
            string? hex = value == Accents[0] ? null : value.Hex;
            if (hex == _draft.AccentColor && !UseWindowsAccent) return;
            _draft.AccentColor = hex;
            RefreshThemeCards();
            Changed(rebuild: false, nameof(SelectedAccent), nameof(UseWindowsAccent));
        }
    }

    public bool UseWindowsAccent
    {
        get => ThemeService.FollowsSystem(_draft);
        set
        {
            if (!value || UseWindowsAccent) return;
            _draft.AccentColor = ThemeService.SystemAccent;
            RefreshThemeCards();
            Changed(rebuild: false, nameof(UseWindowsAccent), nameof(SelectedAccent));
        }
    }

    public bool StartWithWindows
    {
        get => _draft.StartWithWindows;
        set { if (_draft.StartWithWindows == value) return; _draft.StartWithWindows = value; OnPropertyChanged(); }
    }

    public bool ReserveSpace
    {
        get => _draft.ReserveSpace;
        set { if (_draft.ReserveSpace == value) return; _draft.ReserveSpace = value; Changed(rebuild: true, nameof(ReserveSpace)); }
    }

    public bool AutoHide
    {
        get => _draft.AutoHide;
        set { if (_draft.AutoHide == value) return; _draft.AutoHide = value; Changed(rebuild: true, nameof(AutoHide)); }
    }

    public bool OverlayFullscreen
    {
        get => _draft.GamingMode;
        set { if (_draft.GamingMode == value) return; _draft.GamingMode = value; OnPropertyChanged(); }
    }

    /// <summary>Volta os campos deste painel pros padrões de um config novo (ainda sem salvar).</summary>
    public void RestoreDefaults()
    {
        var defaults = new AppConfig();
        _draft.Edge = defaults.Edge;
        _draft.Theme = defaults.Theme;
        _draft.Thickness = defaults.Thickness;
        _draft.BarOpacity = defaults.BarOpacity;
        _draft.AccentColor = defaults.AccentColor;
        _draft.StartWithWindows = defaults.StartWithWindows;
        _draft.ReserveSpace = defaults.ReserveSpace;
        _draft.AutoHide = defaults.AutoHide;
        _draft.GamingMode = defaults.GamingMode;
        RefreshThemeCards();
        Changed(rebuild: true, string.Empty);
    }

    private void RefreshThemeCards()
    {
        foreach (var theme in Themes) theme.Refresh(ThemeService.Preview(_draft, theme.Id));
    }

    private void Changed(bool rebuild, params string[] properties)
    {
        foreach (var p in properties) OnPropertyChanged(p);
        CommandManager.InvalidateRequerySuggested();
        _preview(rebuild);
    }
}
