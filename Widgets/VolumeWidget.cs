using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI.Controls;

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

    private readonly EndpointVolumeService _service;
    private readonly bool _isMic;
    private readonly bool _isOverlay;

    private readonly Icon _icon = WidgetUi.Icon("H.Icon.Volume");
    private readonly Icon _muteIcon = WidgetUi.Icon("H.Icon.Volume");
    // Minimalista: só o ícone; a porcentagem aparece por um instante ao ajustar pela roda.
    private readonly TextBlock _percent = WidgetUi.Text(11, mono: true);
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

        _percent.Margin = new Thickness(4, 0, 0, 0);
        _percent.Visibility = Visibility.Collapsed;
        _content.Children.Add(_icon);
        _content.Children.Add(_percent);

        string name = Strings.WidgetName(isMic ? "mic" : "volume");
        // No overlay o conteúdo vai pra um Border (só exibe) — um elemento só pode ter um pai.
        _toggle = WidgetUi.IconToggleButton(isOverlay ? new Border() : _content, name);
        // Largura automática (mínimo 30): cresce quando a porcentagem aparece ao lado do ícone.
        _toggle.Width = double.NaN;
        _toggle.SetResourceReference(FrameworkElement.MinWidthProperty, "H.Size.IconButton");
        _toggle.Padding = new Thickness(7, 0, 7, 0);

        // No overlay o clique atravessa a janela — só exibe o estado, sem botão.
        if (isOverlay)
        {
            var box = new Border { Child = _content, Padding = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            box.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.IconButton");
            _root = box;
        }
        else
        {
            _root = _toggle;
        }

        _muteButton = WidgetUi.IconButton(_muteIcon, Strings.MediaMute);
        _muteButton.Margin = new Thickness(0, 0, 0, 4);

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
            _icon.Geometry = WidgetUi.Geometry(_isMic ? "H.Icon.MicOff" : "H.Icon.VolumeMute");
            _percent.Text = "—";
            _root.ToolTip = _isMic ? Strings.MicNoDevice : Strings.VolumeNoDevice;
            return;
        }

        int pct = (int)Math.Round(s.Volume * 100);
        _icon.Geometry = WidgetUi.Geometry(IconKey(s.Volume, s.Muted));
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
        _muteIcon.Geometry = WidgetUi.Geometry(IconKey(1f, muted));
        _muteButton.ToolTip = muted ? Strings.MediaUnmute : Strings.MediaMute;
    }

    private string IconKey(float volume, bool muted)
    {
        if (_isMic) return muted ? "H.Icon.MicOff" : "H.Icon.Mic";
        if (muted) return "H.Icon.VolumeMute";

        // Uma onda até 50%, duas acima — como o ícone de volume da taskbar.
        return volume <= 0.5f ? "H.Icon.VolumeLow" : "H.Icon.Volume";
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
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 8, 6, 6),
            Child = panel
        };
        border.SetResourceReference(Border.BackgroundProperty, "H.Brush.SurfacePopup");
        border.SetResourceReference(Border.BorderBrushProperty, "H.Brush.Line");
        border.SetResourceReference(Border.CornerRadiusProperty, "H.Radius.Popup");
        border.SetResourceReference(TextElement.ForegroundProperty, "H.Brush.Text");

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

    public void Dispose()
    {
        _timer.Stop();
        _percentHideTimer.Stop();
        _volumePopup.IsOpen = false;
    }
}
