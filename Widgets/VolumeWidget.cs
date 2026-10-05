using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.Widgets;

/// <summary>
/// Volume de um dispositivo padrão do Windows — saída (widget <c>volume</c>, o volume
/// geral do PC) ou microfone (widget <c>mic</c>): roda do mouse ajusta em passos de 5%,
/// clique abre um popup com slider + mudo (mesmo padrão do volume do <see cref="MediaWidget"/>).
/// Refresh periódico pra refletir mudanças feitas fora da barra (Configurações do
/// Windows, teclas de volume, Discord, troca de dispositivo padrão).
/// </summary>
public sealed class VolumeWidget : IWidget
{
    private const float VolumeStep = 0.05f;

    // Segoe Fluent Icons (Win11) com fallback pra Segoe MDL2 Assets (Win10) — mesmos glifos nas duas.
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphMicOn = "";
    private const string GlyphMicOff = "";
    // Alto-falante com 0 a 3 "ondas" conforme o nível, como o ícone de volume da taskbar.
    private static readonly string[] GlyphSpeakerLevels = { "", "", "", "" };
    private const string GlyphSpeakerMuted = "";

    private readonly EndpointVolumeService _service;
    private readonly bool _isMic;
    private readonly bool _isOverlay;
    private readonly Color _popupBackground;
    private readonly Color _popupForeground;

    private readonly TextBlock _icon = CreateGlyph(GlyphMicOn);
    private readonly TextBlock _muteIcon = CreateGlyph(GlyphMicOn);
    // Minimalista: só o ícone; a porcentagem aparece por um instante ao ajustar pela roda.
    private readonly TextBlock _percent = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 0, 0),
        Visibility = Visibility.Collapsed
    };
    private readonly DispatcherTimer _percentHideTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly StackPanel _content = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly ToggleButton _toggle;
    private readonly FrameworkElement _root;

    private readonly Slider _volumeSlider = new()
    {
        Orientation = Orientation.Vertical,
        Minimum = 0,
        Maximum = 100,
        Height = 90,
        Width = 24,
        Margin = new Thickness(0, 8, 0, 8)
    };
    private readonly Button _muteButton;
    private readonly Popup _volumePopup;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _updatingSliderProgrammatically;
    private bool _vertical;

    public FrameworkElement View => _root;

    /// <param name="isMic">true = microfone padrão; false = saída padrão (volume geral do PC).</param>
    public VolumeWidget(AppConfig cfg, bool isMic, bool isOverlay = false)
    {
        _isMic = isMic;
        _service = isMic ? EndpointVolumeService.Microphone : EndpointVolumeService.Speakers;
        _isOverlay = isOverlay;

        var style = ThemeService.GetEffectiveStyle(cfg);
        _popupBackground = style.Background;
        _popupForeground = style.Foreground;

        var iconBrush = FrozenBrush(_popupForeground);
        _icon.Foreground = iconBrush;
        _muteIcon.Foreground = iconBrush;
        // Dentro do ToggleButton o texto herdaria o Foreground do estilo do botão, não o do tema da barra.
        _percent.Foreground = iconBrush;

        _content.Children.Add(_icon);
        _content.Children.Add(_percent);

        _toggle = new ToggleButton
        {
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(2, 0, 2, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false
        };

        // No overlay o clique atravessa a janela — só exibe o estado, sem botão.
        if (isOverlay)
        {
            _root = new Border { Child = _content, Padding = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        }
        else
        {
            _toggle.Content = _content;
            _root = _toggle;
        }

        _muteButton = new Button
        {
            Content = _muteIcon,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(0, 0, 0, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false
        };

        _volumePopup = BuildVolumePopup();

        // Checked/Unchecked em vez de Binding — mesmo motivo do MediaWidget (bool? vs bool).
        _toggle.Checked += (_, _) => { SyncPopupFromState(); _volumePopup.IsOpen = true; };
        _toggle.Unchecked += (_, _) => _volumePopup.IsOpen = false;
        _volumePopup.Closed += (_, _) => _toggle.IsChecked = false;

        _timer.Tick += (_, _) => Update();
        _percentHideTimer.Tick += (_, _) =>
        {
            _percentHideTimer.Stop();
            _percent.Visibility = Visibility.Collapsed;
        };
    }

    public void ApplyOrientation(Orientation orientation)
    {
        // Barra vertical não tem largura pra porcentagem nem temporária.
        _vertical = orientation == Orientation.Vertical;
        if (_vertical) _percent.Visibility = Visibility.Collapsed;
    }

    private void FlashPercent()
    {
        if (_vertical) return;
        _percent.Visibility = Visibility.Visible;
        _percentHideTimer.Stop();
        _percentHideTimer.Start();
    }

    public void Start()
    {
        if (!_isOverlay) _root.MouseWheel += OnMouseWheel;
        Update();
        _timer.Start();
    }

    private void Update()
    {
        var state = _service.GetState();
        if (state is not { } s)
        {
            _icon.Text = _isMic ? GlyphMicOff : GlyphSpeakerMuted;
            _percent.Text = "—";
            _root.ToolTip = _isMic ? Strings.MicNoDevice : Strings.VolumeNoDevice;
            return;
        }

        int pct = (int)Math.Round(s.Volume * 100);
        _icon.Text = Glyph(s.Volume, s.Muted);
        _percent.Text = s.Muted ? Strings.MediaMutedLabel : $"{pct}%";
        _root.ToolTip = $"{s.Name}\n{(s.Muted ? Strings.MediaMutedLabel : $"{pct}%")}";
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (_service.GetState() is not { } s) return;

        _service.SetVolume(s.Volume + (e.Delta > 0 ? VolumeStep : -VolumeStep));
        Update();
        FlashPercent();
        if (_volumePopup.IsOpen) SyncPopupFromState();
    }

    private void SyncPopupFromState()
    {
        if (_service.GetState() is not { } s) return;

        _updatingSliderProgrammatically = true;
        _volumeSlider.Value = Math.Round(s.Volume * 100);
        _updatingSliderProgrammatically = false;
        RefreshMuteButton(s.Muted);
    }

    private void OnVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSliderProgrammatically) return;

        _service.SetVolume((float)(e.NewValue / 100));
        Update();
    }

    private void OnMuteButtonClick(object sender, RoutedEventArgs e)
    {
        if (_service.GetState() is not { } s) return;

        _service.SetMuted(!s.Muted);
        RefreshMuteButton(!s.Muted);
        Update();
    }

    private void RefreshMuteButton(bool muted)
    {
        // Botão mostra o estado atual; no alto-falante usa o ícone "cheio" quando com som.
        _muteIcon.Text = Glyph(1f, muted);
        _muteButton.ToolTip = muted ? Strings.MediaUnmute : Strings.MediaMute;
    }

    private string Glyph(float volume, bool muted)
    {
        if (_isMic) return muted ? GlyphMicOff : GlyphMicOn;
        if (muted) return GlyphSpeakerMuted;

        // 0% = sem ondas; o resto divide em terços (1–33, 34–66, 67–100).
        int level = volume <= 0.005f ? 0 : Math.Min(3, 1 + (int)(volume * 3 - 0.0001f));
        return GlyphSpeakerLevels[level];
    }

    private Popup BuildVolumePopup()
    {
        _volumeSlider.ValueChanged += OnVolumeSliderChanged;
        _muteButton.Click += OnMuteButtonClick;

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(_volumeSlider);
        panel.Children.Add(_muteButton);

        var border = new Border
        {
            Background = FrozenBrush(_popupBackground),
            BorderBrush = FrozenBrush(Color.FromArgb(0x40, _popupForeground.R, _popupForeground.G, _popupForeground.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 8, 6, 6),
            Child = panel
        };
        TextElement.SetForeground(border, FrozenBrush(_popupForeground));

        return new Popup
        {
            Placement = PlacementMode.Top,
            PlacementTarget = _toggle,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = border
        };
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static TextBlock CreateGlyph(string glyph)
    {
        var text = new TextBlock
        {
            Text = glyph,
            FontFamily = IconFont,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Grayscale AA: ClearType franja em cima de janela translúcida (ver MediaWidget.CreateGlyph).
        TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);

        return text;
    }

    public void Dispose()
    {
        _timer.Stop();
        _percentHideTimer.Stop();
        _volumePopup.IsOpen = false;
    }
}
