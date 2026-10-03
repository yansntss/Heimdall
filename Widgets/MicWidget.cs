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
/// Volume do microfone padrão do Windows: roda do mouse ajusta em passos de 5%, clique
/// abre um popup com slider + mudo (mesmo padrão do volume do <see cref="MediaWidget"/>).
/// Refresh periódico pra refletir mudanças feitas fora da barra (Configurações do
/// Windows, Discord, troca de microfone padrão).
/// </summary>
public sealed class MicWidget : IWidget
{
    private const float VolumeStep = 0.05f;

    // Segoe Fluent Icons (Win11) com fallback pra Segoe MDL2 Assets (Win10) — mesmos glifos nas duas.
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphMicOn = "";
    private const string GlyphMicOff = "";

    private readonly bool _isOverlay;
    private readonly Color _popupBackground;
    private readonly Color _popupForeground;

    private readonly TextBlock _icon = CreateGlyph(GlyphMicOn);
    private readonly TextBlock _muteIcon = CreateGlyph(GlyphMicOn);
    private readonly TextBlock _percent = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 0, 0)
    };
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

    public MicWidget(AppConfig cfg, bool isOverlay = false)
    {
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
    }

    public void ApplyOrientation(Orientation orientation)
    {
        // Barra vertical: só o ícone, porcentagem vai pro tooltip.
        _vertical = orientation == Orientation.Vertical;
        _percent.Visibility = _vertical ? Visibility.Collapsed : Visibility.Visible;
        if (_timer.IsEnabled) Update();
    }

    public void Start()
    {
        if (!_isOverlay) _root.MouseWheel += OnMouseWheel;
        Update();
        _timer.Start();
    }

    private void Update()
    {
        var state = MicVolumeService.GetState();
        if (state is not { } s)
        {
            _icon.Text = GlyphMicOff;
            _percent.Text = "—";
            _root.ToolTip = Strings.MicNoDevice;
            return;
        }

        int pct = (int)Math.Round(s.Volume * 100);
        _icon.Text = s.Muted ? GlyphMicOff : GlyphMicOn;
        _percent.Text = s.Muted ? Strings.MediaMutedLabel : $"{pct}%";
        _root.ToolTip = $"{s.Name}\n{(s.Muted ? Strings.MediaMutedLabel : $"{pct}%")}";
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (MicVolumeService.GetState() is not { } s) return;

        MicVolumeService.SetVolume(s.Volume + (e.Delta > 0 ? VolumeStep : -VolumeStep));
        Update();
        if (_volumePopup.IsOpen) SyncPopupFromState();
    }

    private void SyncPopupFromState()
    {
        if (MicVolumeService.GetState() is not { } s) return;

        _updatingSliderProgrammatically = true;
        _volumeSlider.Value = Math.Round(s.Volume * 100);
        _updatingSliderProgrammatically = false;
        RefreshMuteButton(s.Muted);
    }

    private void OnVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSliderProgrammatically) return;

        MicVolumeService.SetVolume((float)(e.NewValue / 100));
        Update();
    }

    private void OnMuteButtonClick(object sender, RoutedEventArgs e)
    {
        if (MicVolumeService.GetState() is not { } s) return;

        MicVolumeService.SetMuted(!s.Muted);
        RefreshMuteButton(!s.Muted);
        Update();
    }

    private void RefreshMuteButton(bool muted)
    {
        _muteIcon.Text = muted ? GlyphMicOff : GlyphMicOn;
        _muteButton.ToolTip = muted ? Strings.MediaUnmute : Strings.MediaMute;
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
        _volumePopup.IsOpen = false;
    }
}
