using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI.Controls;
using Windows.Media.Control;

namespace Heimdall.Widgets;

/// <summary>
/// Controle de mídia via SMTC (Spotify/YouTube/qualquer player) — API nativa do
/// Windows, sem login nem API key do serviço. Volume por app via NAudio, já que
/// o SMTC não expõe controle de volume.
/// </summary>
public sealed class MediaWidget : IWidget
{
    private const double TextWidth = 170;
    private const double CoverSize = 22;
    private const double ControlSize = 22;
    private const float VolumeStep = 0.05f;

    private const string IconPlay = "H.Icon.Play";
    private const string IconPause = "H.Icon.Pause";
    private const string IconVolumeOn = "H.Icon.Volume";
    private const string IconVolumeMuted = "H.Icon.VolumeMute";

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private readonly Icon _previousIcon = WidgetUi.Icon("H.Icon.Previous", 12, filled: true);
    private readonly Icon _playPauseIcon = WidgetUi.Icon(IconPlay, 12, filled: true);
    private readonly Icon _nextIcon = WidgetUi.Icon("H.Icon.Next", 12, filled: true);
    private readonly Icon _volumeIcon = WidgetUi.Icon(IconVolumeOn, 13);
    private readonly Icon _muteIcon = WidgetUi.Icon(IconVolumeOn, 16);

    private readonly Button _previous;
    private readonly Button _playPause;
    private readonly Button _next;
    private readonly ToggleButton _volume;

    // Capa 22x22 com raio 6 — Border.CornerRadius não recorta o filho, daí o Clip.
    private readonly Image _thumbnail = new()
    {
        Width = CoverSize,
        Height = CoverSize,
        VerticalAlignment = VerticalAlignment.Center,
        Stretch = Stretch.UniformToFill,
        Clip = new RectangleGeometry(new Rect(0, 0, CoverSize, CoverSize), 6, 6),
        Margin = new Thickness(0, 0, 6, 0),
        Visibility = Visibility.Collapsed
    };

    private readonly TranslateTransform _textScroll = new();
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Run _title = new() { FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
    private readonly Run _artist = new() { FontSize = 11 };

    /// <summary>Pill (raio 999, altura 28) que envolve capa, texto e controles.</summary>
    private readonly Border _chip = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };

    private const double EqBarMinHeight = 3;
    private const double EqBarMaxHeight = 11;
    private readonly Rectangle _eqBar1 = new() { Width = 2, VerticalAlignment = VerticalAlignment.Bottom, Height = EqBarMinHeight, Margin = new Thickness(0, 0, 1, 0) };
    private readonly Rectangle _eqBar2 = new() { Width = 2, VerticalAlignment = VerticalAlignment.Bottom, Height = EqBarMaxHeight, Margin = new Thickness(0, 0, 1, 0) };
    private readonly Rectangle _eqBar3 = new() { Width = 2, VerticalAlignment = VerticalAlignment.Bottom, Height = EqBarMinHeight };
    private readonly StackPanel _equalizer = new()
    {
        Orientation = Orientation.Horizontal,
        Height = EqBarMaxHeight,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 4, 0),
        Visibility = Visibility.Collapsed
    };
    private bool _equalizerRunning;

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
        VerticalAlignment = VerticalAlignment.Center
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
    private readonly Button _muteButton;
    private readonly Popup _volumePopup;

    private readonly TextBlock _feedbackText = WidgetUi.Text(12, mono: true);
    private readonly Popup _feedbackPopup;
    private readonly DispatcherTimer _feedbackTimer;
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private bool _updatingSliderProgrammatically;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    private readonly bool _narrowBar;
    private readonly bool _isOverlay;

    public FrameworkElement View => _chip;

    /// <param name="narrowBar">Barra horizontal num monitor em pé: versão mínima — capa, play/pause e próxima; título só no tooltip, volume só pela roda do mouse.</param>
    public MediaWidget(AppConfig cfg, bool isOverlay = false, bool narrowBar = false)
    {
        _narrowBar = narrowBar;
        _isOverlay = isOverlay;

        _previous = WidgetUi.IconButton(_previousIcon, Strings.MediaPrevious, ControlSize);
        _playPause = WidgetUi.IconButton(_playPauseIcon, Strings.MediaPlayPauseTooltip, ControlSize);
        _next = WidgetUi.IconButton(_nextIcon, Strings.MediaNext, ControlSize);
        _volume = WidgetUi.IconToggleButton(_volumeIcon, Strings.MediaVolume);
        _volume.Width = ControlSize;
        _volume.Height = ControlSize;
        _muteButton = WidgetUi.IconButton(_muteIcon, Strings.MediaMute);

        _progressBar.SetResourceReference(Panel.BackgroundProperty, "H.Brush.Track");
        _progressFill.SetResourceReference(Border.BackgroundProperty, "H.Brush.Accent");
        _progressBar.Children.Add(_progressFill);

        foreach (var bar in new[] { _eqBar1, _eqBar2, _eqBar3 })
        {
            bar.SetResourceReference(Shape.FillProperty, "H.Brush.Accent");
            _equalizer.Children.Add(bar);
        }

        // Título semibold 11.5 + artista 11 em texto secundário, na mesma linha.
        _artist.SetResourceReference(TextElement.ForegroundProperty, "H.Brush.TextSecondary");
        _text.Inlines.Add(_title);
        _text.Inlines.Add(new Run("  "));
        _text.Inlines.Add(_artist);
        _text.RenderTransform = _textScroll;
        _textStack.Children.Add(_text);
        _textStack.Children.Add(_progressBar);
        _textClip.Child = _textStack;
        _textClip.MouseEnter += OnTextMouseEnter;
        _textClip.MouseLeave += OnTextMouseLeave;

        _root.Children.Add(_thumbnail);
        _root.Children.Add(_equalizer);
        _root.Children.Add(_textClip);

        // No overlay (jogo em tela cheia) a janela é click-through — botões nunca seriam
        // clicáveis ali, então nem aparecem. Só o texto/capa/progresso da faixa.
        if (!isOverlay)
        {
            _root.Children.Add(_previous);
            _root.Children.Add(_playPause);
            _root.Children.Add(_next);
            _root.Children.Add(_volume);
        }

        // Pill só na barra normal: no overlay o fundo do jogo aparece direto, com contorno no texto.
        _chip.Child = _root;
        // "Raio 999" do design = metade do lado menor: o WPF não limita o raio sozinho e um
        // 999 literal vira elipse.
        _chip.SizeChanged += (_, e) =>
        {
            _chip.CornerRadius = new CornerRadius(Math.Min(e.NewSize.Width, e.NewSize.Height) / 2);
            if (e.HeightChanged && _root.Orientation == Orientation.Horizontal) FitToChipHeight(e.NewSize.Height);
        };
        if (!isOverlay) _chip.SetResourceReference(Border.BackgroundProperty, "H.Brush.Selected");

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

    /// <summary>Capa e botões (22 no design) encolhem quando o chip é mais baixo — Compacta ou barra afinada.</summary>
    private void FitToChipHeight(double height)
    {
        double cover = Math.Min(CoverSize, height - 4);
        double control = Math.Min(ControlSize, height);
        if (cover <= 0 || control <= 0) return;

        _thumbnail.Width = cover;
        _thumbnail.Height = cover;
        _thumbnail.Clip = new RectangleGeometry(new Rect(0, 0, cover, cover), cover * 6 / CoverSize, cover * 6 / CoverSize);
        foreach (var button in new FrameworkElement[] { _previous, _playPause, _next, _volume })
        {
            button.Width = control;
            button.Height = control;
        }
    }

    public void ApplyOrientation(Orientation orientation)
    {
        bool vertical = orientation == Orientation.Vertical;
        _root.Orientation = orientation;

        // Pill de 28 na horizontal (capa encostada na borda esquerda, 3px de respiro);
        // na vertical vira cápsula em pé, com a largura do chip.
        if (vertical)
        {
            _chip.Height = double.NaN;
            _chip.SetResourceReference(FrameworkElement.WidthProperty, "H.Size.Chip");
            _chip.Padding = new Thickness(0, 4, 0, 4);
            _thumbnail.Margin = new Thickness(0, 0, 0, 4);
        }
        else
        {
            _chip.Width = double.NaN;
            _chip.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.Chip");
            _chip.Padding = _isOverlay ? new Thickness(0) : new Thickness(3, 0, 4, 0);
        }

        // Barra vertical: sem espaço pro texto — capa e botões empilhados,
        // texto só no tooltip (setado em RefreshTextAsync).
        _textClip.Visibility = vertical || _narrowBar ? Visibility.Collapsed : Visibility.Visible;

        if (_narrowBar)
        {
            _previous.Visibility = Visibility.Collapsed;
            _volume.Visibility = Visibility.Collapsed;
        }
    }

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
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            HookSession();
        }
        catch
        {
            // SMTC indisponível: widget fica oculto, sem derrubar o app.
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
        _dispatcher.BeginInvoke(new Action(HookSession));

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

        bool playing = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        _playPauseIcon.Geometry = WidgetUi.Geometry(playing ? IconPause : IconPlay);
        _playPause.ToolTip = playing ? Strings.MediaPause : Strings.MediaPlay;

        if (playing) StartEqualizer(); else StopEqualizer();

        RefreshVolumeIcon();
        UpdateProgress();
    }

    // ---------- Ícone de equalizador (animado enquanto a faixa está tocando) ----------

    private void StartEqualizer()
    {
        _equalizer.Visibility = Visibility.Visible;
        if (_equalizerRunning) return;
        _equalizerRunning = true;

        AnimateBar(_eqBar1, 0.42, TimeSpan.Zero);
        AnimateBar(_eqBar2, 0.36, TimeSpan.FromMilliseconds(120));
        AnimateBar(_eqBar3, 0.5, TimeSpan.FromMilliseconds(60));
    }

    private void StopEqualizer()
    {
        _equalizerRunning = false;
        _equalizer.Visibility = Visibility.Collapsed;
        _eqBar1.BeginAnimation(FrameworkElement.HeightProperty, null);
        _eqBar2.BeginAnimation(FrameworkElement.HeightProperty, null);
        _eqBar3.BeginAnimation(FrameworkElement.HeightProperty, null);
    }

    private static void AnimateBar(Rectangle bar, double seconds, TimeSpan beginTime)
    {
        var animation = new DoubleAnimation(EqBarMinHeight, EqBarMaxHeight, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = beginTime,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        bar.BeginAnimation(FrameworkElement.HeightProperty, animation);
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
            _dispatcher.Invoke(() => _chip.Visibility = Visibility.Collapsed);
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
                    _chip.Visibility = Visibility.Collapsed;
                    return;
                }
                _title.Text = string.IsNullOrWhiteSpace(title) ? artist : title;
                _artist.Text = string.IsNullOrWhiteSpace(title) ? "" : artist;
                _root.ToolTip = full; // barra vertical: texto some, sobra só o tooltip
                _thumbnail.Source = thumbnail;
                _thumbnail.Visibility = thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
                _chip.Visibility = Visibility.Visible;

                StopMarquee();
                if (_textClip.IsMouseOver) StartMarquee();
            });
        }
        catch
        {
            _dispatcher.Invoke(() => _chip.Visibility = Visibility.Collapsed);
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
        _volumeIcon.Geometry = WidgetUi.Geometry(muted ? IconVolumeMuted : IconVolumeOn);
    }

    private void RefreshMuteButtonGlyph(bool muted)
    {
        _muteIcon.Geometry = WidgetUi.Geometry(muted ? IconVolumeMuted : IconVolumeOn);
        _muteButton.ToolTip = muted ? Strings.MediaUnmute : Strings.MediaMute;
    }

    private void ShowVolumeFeedback(float volume, bool muted)
    {
        _feedbackText.Text = muted ? Strings.MediaMutedLabel : $"{Math.Round(volume * 100)}%";
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

        return new Popup
        {
            Placement = PlacementMode.Top,
            PlacementTarget = _volume,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = PopupSurface(panel, new Thickness(6, 8, 6, 6), "H.Radius.Popup")
        };
    }

    private Popup BuildFeedbackPopup() => new()
    {
        Placement = PlacementMode.Top,
        StaysOpen = true,
        AllowsTransparency = true,
        IsHitTestVisible = false,
        Child = PopupSurface(_feedbackText, new Thickness(8, 3, 8, 3), "H.Radius.Chip")
    };

    /// <summary>Superfície de popup do tema (#12161E @ 94%, borda 9%) — ao vivo via DynamicResource.</summary>
    private static Border PopupSurface(UIElement child, Thickness padding, string radiusKey)
    {
        var border = new Border { BorderThickness = new Thickness(1), Padding = padding, Child = child };
        border.SetResourceReference(Border.BackgroundProperty, "H.Brush.SurfacePopup");
        border.SetResourceReference(Border.BorderBrushProperty, "H.Brush.Line");
        border.SetResourceReference(Border.CornerRadiusProperty, radiusKey);
        border.SetResourceReference(TextElement.ForegroundProperty, "H.Brush.Text");
        border.SetResourceReference(TextElement.FontFamilyProperty, "H.Font.UI");
        return border;
    }

    public void Dispose()
    {
        _feedbackTimer.Stop();
        _progressTimer.Stop();
        StopMarquee();
        StopEqualizer();
        _volumePopup.IsOpen = false;
        _feedbackPopup.IsOpen = false;

        // Sem isso, o GlobalSystemMediaTransportControlsSessionManager (que sobrevive além
        // do widget, gerenciado pelo próprio SO) mantém viva a inscrição — e com ela, esse
        // MediaWidget inteiro (StackPanel, timers já parados mas ainda referenciados, etc.)
        // — um "zumbi" que nunca é coletado pelo GC a cada Reload() com o widget de mídia
        // configurado.
        if (_manager is not null) _manager.CurrentSessionChanged -= OnCurrentSessionChanged;

        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionEvent;
            _session.PlaybackInfoChanged -= OnSessionEvent;
            _session.TimelinePropertiesChanged -= OnSessionEvent;
        }
    }
}
