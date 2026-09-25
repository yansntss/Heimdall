using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using Heimdall.Config;
using Heimdall.Services;
using Windows.Media.Control;

namespace Heimdall.Widgets;

/// <summary>
/// Controle de mídia via SMTC (Spotify/YouTube/qualquer player) — API nativa do
/// Windows, sem login nem API key do serviço. Volume por app via NAudio, já que
/// o SMTC não expõe controle de volume.
/// </summary>
public sealed class MediaWidget : IWidget
{
    private const double TextWidth = 140;
    private const float VolumeStep = 0.05f;

    // Segoe Fluent Icons (Win11) com fallback pra Segoe MDL2 Assets (Win10) — mesmos glifos nas duas.
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphPrevious = "";
    private const string GlyphNext = "";
    private const string GlyphPlay = "";
    private const string GlyphPause = "";
    private const string GlyphVolumeOn = "";
    private const string GlyphVolumeMuted = "";

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Color _popupBackground;
    private readonly Color _popupForeground;

    private readonly Button _previous = CreateIconButton(GlyphPrevious);
    private readonly Button _playPause = CreateIconButton(GlyphPlay);
    private readonly Button _next = CreateIconButton(GlyphNext);
    private readonly ToggleButton _volume = CreateIconToggleButton(GlyphVolumeOn);

    private readonly Image _thumbnail = new()
    {
        Width = 16,
        Height = 16,
        VerticalAlignment = VerticalAlignment.Center,
        Stretch = Stretch.UniformToFill,
        Visibility = Visibility.Collapsed
    };

    private readonly TranslateTransform _textScroll = new();
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly Border _progressFill = new() { Height = 2, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
    private readonly Grid _progressBar = new()
    {
        Height = 2,
        Margin = new Thickness(0, 2, 0, 0),
        Visibility = Visibility.Collapsed
    };

    private readonly StackPanel _textStack = new();

    private readonly Border _textClip = new()
    {
        Width = TextWidth,
        ClipToBounds = true,
        Margin = new Thickness(4, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
        // Transparent (não null): Background=null deixa a área "vazia" do Border sem
        // hit-test, então MouseEnter/Leave só disparariam em cima dos glifos do texto.
        Background = Brushes.Transparent
    };

    private readonly StackPanel _root = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    private readonly Slider _volumeSlider = new()
    {
        Orientation = Orientation.Vertical,
        Minimum = 0,
        Maximum = 100,
        Height = 90,
        Width = 24,
        Margin = new Thickness(0, 8, 0, 8)
    };
    private readonly Button _muteButton = CreateIconButton(GlyphVolumeOn);
    private readonly Popup _volumePopup;

    private readonly TextBlock _feedbackText = new() { FontSize = 12 };
    private readonly Popup _feedbackPopup;
    private readonly DispatcherTimer _feedbackTimer;
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private bool _updatingSliderProgrammatically;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    public FrameworkElement View => _root;

    public MediaWidget(AppConfig cfg)
    {
        var style = ThemeService.GetEffectiveStyle(cfg);
        _popupBackground = style.Background;
        _popupForeground = style.Foreground;

        _progressBar.Background = FrozenBrush(Color.FromArgb(0x33, _popupForeground.R, _popupForeground.G, _popupForeground.B));
        _progressFill.Background = FrozenBrush(style.Accent);
        _progressBar.Children.Add(_progressFill);

        _text.RenderTransform = _textScroll;
        _textStack.Children.Add(_text);
        _textStack.Children.Add(_progressBar);
        _textClip.Child = _textStack;
        _textClip.MouseEnter += OnTextMouseEnter;
        _textClip.MouseLeave += OnTextMouseLeave;

        _root.Children.Add(_thumbnail);
        _root.Children.Add(_textClip);
        _root.Children.Add(_previous);
        _root.Children.Add(_playPause);
        _root.Children.Add(_next);
        _root.Children.Add(_volume);

        _volumePopup = BuildVolumePopup();

        // Checked/Unchecked (não Binding pro Popup.IsOpen): IsChecked é bool? e Popup.IsOpen
        // é bool — o binding entre os dois falha silenciosamente por causa do descasamento
        // de tipo. ToggleButton + StaysOpen=false é o mesmo padrão do ComboBox: fecha
        // corretamente ao clicar de novo no próprio ícone, sem o Popup engolir o clique.
        _volume.Checked += (_, _) => { SyncVolumePopupFromState(); _volumePopup.IsOpen = true; };
        _volume.Unchecked += (_, _) => _volumePopup.IsOpen = false;
        _volumePopup.Closed += (_, _) => _volume.IsChecked = false;

        _feedbackPopup = BuildFeedbackPopup();

        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); _feedbackPopup.IsOpen = false; };
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start()
    {
        _previous.Click += (_, _) => _ = _session?.TrySkipPreviousAsync();
        _playPause.Click += (_, _) => _ = _session?.TryTogglePlayPauseAsync();
        _next.Click += (_, _) => _ = _session?.TrySkipNextAsync();
        _root.MouseWheel += OnMouseWheel;
        _progressTimer.Tick += (_, _) => UpdateProgress();
        _progressTimer.Start();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => _dispatcher.BeginInvoke(new Action(HookSession));
            HookSession();
        }
        catch
        {
            // SMTC indisponível: widget fica oculto, sem derrubar o app.
        }
    }

    private void HookSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionEvent;
            _session.PlaybackInfoChanged -= OnSessionEvent;
            _session.TimelinePropertiesChanged -= OnSessionEvent;
        }

        _session = _manager?.GetCurrentSession();

        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnSessionEvent;
            _session.PlaybackInfoChanged += OnSessionEvent;
            _session.TimelinePropertiesChanged += OnSessionEvent;
        }

        RefreshControls();
        _ = RefreshTextAsync();
    }

    private void OnSessionEvent(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _dispatcher.BeginInvoke(new Action(() =>
        {
            RefreshControls();
            _ = RefreshTextAsync();
        }));

    /// <summary>Habilita/desabilita os botões e troca o glifo de play/pause conforme o estado real da sessão.</summary>
    private void RefreshControls()
    {
        var info = _session?.GetPlaybackInfo();
        var controls = info?.Controls;

        SetEnabled(_previous, controls?.IsPreviousEnabled ?? false);
        SetEnabled(_next, controls?.IsNextEnabled ?? false);
        SetEnabled(_playPause, (controls?.IsPlayEnabled ?? false) || (controls?.IsPauseEnabled ?? false));

        _playPause.Content = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            ? GlyphPause
            : GlyphPlay;

        RefreshVolumeIcon();
        UpdateProgress();
    }

    /// <summary>
    /// Recalcula a barra de progresso. GetTimelineProperties() é síncrono e só reflete
    /// a posição no momento da última atualização do player — por isso soma o tempo
    /// corrido desde LastUpdatedTime quando está tocando, e o timer de 1s chama isso de
    /// novo pra a barra avançar sozinha entre eventos do SMTC.
    /// </summary>
    private void UpdateProgress()
    {
        var session = _session;
        if (session is null) { _progressBar.Visibility = Visibility.Collapsed; return; }

        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline;
        try { timeline = session.GetTimelineProperties(); }
        catch { _progressBar.Visibility = Visibility.Collapsed; return; }

        var total = timeline.EndTime - timeline.StartTime;
        if (total <= TimeSpan.Zero)
        {
            _progressBar.Visibility = Visibility.Collapsed;
            return;
        }

        bool playing = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var elapsedSinceUpdate = playing ? DateTime.Now - timeline.LastUpdatedTime : TimeSpan.Zero;
        var position = timeline.Position - timeline.StartTime + elapsedSinceUpdate;

        double fraction = Math.Clamp(position / total, 0, 1);
        _progressBar.Visibility = Visibility.Visible;
        _progressFill.Width = TextWidth * fraction;
    }

    private static void SetEnabled(Button button, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1.0 : 0.35;
    }

    private async Task RefreshTextAsync()
    {
        var session = _session;
        if (session is null)
        {
            _dispatcher.Invoke(() => _root.Visibility = Visibility.Collapsed);
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            string title = props.Title ?? "";
            string artist = props.Artist ?? "";
            string full = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} — {title}";
            var thumbnail = await LoadThumbnailAsync(props.Thumbnail);

            _dispatcher.Invoke(() =>
            {
                if (string.IsNullOrWhiteSpace(full))
                {
                    _root.Visibility = Visibility.Collapsed;
                    return;
                }
                _text.Text = full;
                _thumbnail.Source = thumbnail;
                _thumbnail.Visibility = thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
                _root.Visibility = Visibility.Visible;

                StopMarquee();
                if (_textClip.IsMouseOver) StartMarquee();
            });
        }
        catch
        {
            _dispatcher.Invoke(() => _root.Visibility = Visibility.Collapsed);
        }
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnailRef)
    {
        if (thumbnailRef is null) return null;

        try
        {
            using var stream = await thumbnailRef.OpenReadAsync();

            // Sem AsStream()/AsStreamForRead() disponível nessa projeção do WinRT — lê os
            // bytes crus via DataReader e monta o BitmapImage a partir de um MemoryStream.
            using var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);

            using var memory = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memory;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            // Player não forneceu uma capa decodificável — widget segue sem ela.
            return null;
        }
    }

    // ---------- Marquee (rolagem do título/artista no hover, só quando não cabe) ----------

    private bool _marqueeRunning;

    private void OnTextMouseEnter(object sender, MouseEventArgs e) => StartMarquee();

    private void OnTextMouseLeave(object sender, MouseEventArgs e) => StopMarquee();

    private void StartMarquee()
    {
        StopMarquee();

        _text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double overflow = _text.DesiredSize.Width - _textClip.Width;
        if (overflow <= 0) return;

        double scrollSeconds = Math.Clamp(overflow / 40.0, 1.5, 12.0);
        var pause = TimeSpan.FromSeconds(0.8);
        var scroll = TimeSpan.FromSeconds(scrollSeconds);

        var animation = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(pause)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(pause + scroll)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(pause + scroll + pause)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(pause + scroll + pause + scroll)));

        // Freezable.BeginAnimation direto na property, sem Storyboard/SetTarget — mais
        // simples e confiável pra animar um Transform isolado (não preso a um FrameworkElement).
        _textScroll.BeginAnimation(TranslateTransform.XProperty, animation);
        _marqueeRunning = true;
    }

    private void StopMarquee()
    {
        if (!_marqueeRunning) return;
        _textScroll.BeginAnimation(TranslateTransform.XProperty, null);
        _textScroll.X = 0;
        _marqueeRunning = false;
    }

    // ---------- Volume (NAudio Core Audio API por app, com fallback pro master) ----------

    private string? CurrentAppUserModelId => _session?.SourceAppUserModelId;

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        string? app = CurrentAppUserModelId;

        float current = AudioVolumeService.GetVolume(app);
        float next = Math.Clamp(current + (e.Delta > 0 ? VolumeStep : -VolumeStep), 0f, 1f);
        AudioVolumeService.SetVolume(app, next);

        RefreshVolumeIcon();
        ShowVolumeFeedback(next, AudioVolumeService.GetMuted(app));
    }

    /// <summary>
    /// Sincroniza slider e glifo de mudo com o estado real ao abrir o popup. O abrir/fechar
    /// em si é feito pelo binding Popup.IsOpen ↔ ToggleButton.IsChecked (mesmo padrão do
    /// ComboBox) — StaysOpen=false engole o clique de reabertura se isso for feito na mão
    /// via Click/PreviewMouseDown do próprio botão que é o PlacementTarget do popup.
    /// </summary>
    private void SyncVolumePopupFromState()
    {
        string? app = CurrentAppUserModelId;
        _updatingSliderProgrammatically = true;
        _volumeSlider.Value = Math.Round(AudioVolumeService.GetVolume(app) * 100);
        _updatingSliderProgrammatically = false;
        RefreshMuteButtonGlyph(AudioVolumeService.GetMuted(app));
    }

    private void OnVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSliderProgrammatically) return;

        string? app = CurrentAppUserModelId;
        AudioVolumeService.SetVolume(app, (float)(e.NewValue / 100));
        RefreshVolumeIcon();
    }

    private void OnMuteButtonClick(object sender, RoutedEventArgs e)
    {
        string? app = CurrentAppUserModelId;
        bool muted = !AudioVolumeService.GetMuted(app);
        AudioVolumeService.SetMuted(app, muted);
        RefreshMuteButtonGlyph(muted);
        RefreshVolumeIcon();
    }

    private void RefreshVolumeIcon()
    {
        bool muted = AudioVolumeService.GetMuted(CurrentAppUserModelId);
        _volume.Content = muted ? GlyphVolumeMuted : GlyphVolumeOn;
    }

    private void RefreshMuteButtonGlyph(bool muted) => _muteButton.Content = muted ? GlyphVolumeMuted : GlyphVolumeOn;

    private void ShowVolumeFeedback(float volume, bool muted)
    {
        _feedbackText.Text = muted ? "Mudo" : $"{Math.Round(volume * 100)}%";
        _feedbackPopup.PlacementTarget = _root;
        _feedbackPopup.IsOpen = true;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    private Popup BuildVolumePopup()
    {
        _volumeSlider.ValueChanged += OnVolumeSliderChanged;
        _muteButton.Click += OnMuteButtonClick;
        _muteButton.Margin = new Thickness(0, 0, 0, 4);

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
            PlacementTarget = _volume,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = border
        };
    }

    private Popup BuildFeedbackPopup()
    {
        var border = new Border
        {
            Background = FrozenBrush(_popupBackground),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Child = _feedbackText
        };
        TextElement.SetForeground(border, FrozenBrush(_popupForeground));

        return new Popup
        {
            Placement = PlacementMode.Top,
            StaysOpen = true,
            AllowsTransparency = true,
            IsHitTestVisible = false,
            Child = border
        };
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Button CreateIconButton(string glyph) => new()
    {
        Content = glyph,
        FontFamily = IconFont,
        FontSize = 13,
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(2, 0, 2, 0),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Focusable = false
    };

    private static ToggleButton CreateIconToggleButton(string glyph) => new()
    {
        Content = glyph,
        FontFamily = IconFont,
        FontSize = 13,
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(2, 0, 2, 0),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Focusable = false
    };

    public void Dispose()
    {
        _feedbackTimer.Stop();
        _progressTimer.Stop();
        StopMarquee();
        _volumePopup.IsOpen = false;
        _feedbackPopup.IsOpen = false;

        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionEvent;
            _session.PlaybackInfoChanged -= OnSessionEvent;
            _session.TimelinePropertiesChanged -= OnSessionEvent;
        }
    }
}
