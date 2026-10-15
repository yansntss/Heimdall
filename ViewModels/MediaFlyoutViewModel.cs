using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Services;
using Heimdall.Widgets;
using Windows.Media.Control;

namespace Heimdall.ViewModels;

/// <summary>
/// Dropdown de mídia: capa, título/artista, seekbar, controles e volume do app (NAudio,
/// com fallback pro volume geral). Acompanha a sessão SMTC enquanto está aberto.
/// </summary>
internal sealed class MediaFlyoutViewModel : ObservableObject, IDisposable
{
    private readonly GlobalSystemMediaTransportControlsSession _session;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private string _title = "", _artist = "", _positionText = "0:00", _durationText = "0:00";
    private ImageSource? _cover;
    private bool _isPlaying, _canPrevious, _canNext, _canPlayPause, _hasTimeline, _isMuted;
    private double _position, _duration, _volume;
    private bool _seeking;

    public MediaFlyoutViewModel(GlobalSystemMediaTransportControlsSession session)
    {
        _session = session;

        PreviousCommand = new RelayCommand(() => _ = _session.TrySkipPreviousAsync(), () => CanPrevious);
        PlayPauseCommand = new RelayCommand(() => _ = _session.TryTogglePlayPauseAsync(), () => CanPlayPause);
        NextCommand = new RelayCommand(() => _ = _session.TrySkipNextAsync(), () => CanNext);
        ToggleMuteCommand = new RelayCommand(() =>
        {
            AudioVolumeService.SetMuted(AppId, !IsMuted);
            RefreshVolume();
        });

        _session.MediaPropertiesChanged += OnSessionEvent;
        _session.PlaybackInfoChanged += OnSessionEvent;
        _session.TimelinePropertiesChanged += OnSessionEvent;
        _timer.Tick += (_, _) => RefreshTimeline();
        _timer.Start();

        RefreshPlayback();
        RefreshTimeline();
        RefreshVolume();
        _ = RefreshMediaAsync();
    }

    private string AppId => _session.SourceAppUserModelId;

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Artist { get => _artist; private set => SetProperty(ref _artist, value); }
    public ImageSource? Cover { get => _cover; private set => SetProperty(ref _cover, value); }
    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }
    public bool CanPrevious { get => _canPrevious; private set => SetProperty(ref _canPrevious, value); }
    public bool CanNext { get => _canNext; private set => SetProperty(ref _canNext, value); }
    public bool CanPlayPause { get => _canPlayPause; private set => SetProperty(ref _canPlayPause, value); }
    public bool HasTimeline { get => _hasTimeline; private set => SetProperty(ref _hasTimeline, value); }
    public string PositionText { get => _positionText; private set => SetProperty(ref _positionText, value); }
    public string DurationText { get => _durationText; private set => SetProperty(ref _durationText, value); }
    public double Duration { get => _duration; private set => SetProperty(ref _duration, value); }

    /// <summary>Segundos. A view escreve aqui enquanto o usuário arrasta; a busca só vai pro player em <see cref="CommitSeek"/>.</summary>
    public double Position
    {
        get => _position;
        set
        {
            if (!SetProperty(ref _position, value)) return;
            PositionText = Format(value);
        }
    }

    /// <summary>0–100, já aplicado no volume do app.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (!SetProperty(ref _volume, Math.Clamp(value, 0, 100))) return;
            AudioVolumeService.SetVolume(AppId, (float)(_volume / 100));
            if (IsMuted && _volume > 0)
            {
                AudioVolumeService.SetMuted(AppId, false);
                IsMuted = false;
            }
        }
    }

    public bool IsMuted { get => _isMuted; private set => SetProperty(ref _isMuted, value); }

    public ICommand PreviousCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand ToggleMuteCommand { get; }

    /// <summary>Usuário pegou a seekbar: o timer para de mexer na posição até soltar.</summary>
    public void BeginSeek() => _seeking = true;

    public void CommitSeek()
    {
        _seeking = false;
        var timeline = _session.GetTimelineProperties();
        var target = timeline.StartTime + TimeSpan.FromSeconds(Position);
        _ = _session.TryChangePlaybackPositionAsync(target.Ticks);
    }

    private void OnSessionEvent(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _dispatcher.BeginInvoke(new Action(() =>
        {
            RefreshPlayback();
            RefreshTimeline();
            _ = RefreshMediaAsync();
        }));

    private void RefreshPlayback()
    {
        try
        {
            var info = _session.GetPlaybackInfo();
            var controls = info?.Controls;
            IsPlaying = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            CanPrevious = controls?.IsPreviousEnabled ?? false;
            CanNext = controls?.IsNextEnabled ?? false;
            CanPlayPause = (controls?.IsPlayEnabled ?? false) || (controls?.IsPauseEnabled ?? false);
            CommandManager.InvalidateRequerySuggested();
        }
        catch { /* sessão encerrada no meio — fica com o último estado */ }
    }

    /// <summary>Mesma conta do widget: posição da última atualização do player + tempo corrido desde então.</summary>
    private void RefreshTimeline()
    {
        if (_seeking) return;
        try
        {
            var timeline = _session.GetTimelineProperties();
            var total = timeline.EndTime - timeline.StartTime;
            HasTimeline = total > TimeSpan.Zero;
            if (!HasTimeline) return;

            var elapsed = IsPlaying ? DateTime.Now - timeline.LastUpdatedTime : TimeSpan.Zero;
            var position = timeline.Position - timeline.StartTime + elapsed;
            Duration = total.TotalSeconds;
            DurationText = Format(total.TotalSeconds);
            Position = Math.Clamp(position.TotalSeconds, 0, total.TotalSeconds);
        }
        catch { HasTimeline = false; }
    }

    private void RefreshVolume()
    {
        _volume = Math.Round(AudioVolumeService.GetVolume(AppId) * 100);
        OnPropertyChanged(nameof(Volume));
        IsMuted = AudioVolumeService.GetMuted(AppId);
    }

    private async Task RefreshMediaAsync()
    {
        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            var cover = await MediaWidget.LoadThumbnailAsync(props.Thumbnail);
            Title = string.IsNullOrWhiteSpace(props.Title) ? Strings.MediaNothingPlaying : props.Title;
            Artist = props.Artist ?? "";
            Cover = cover;
        }
        catch { /* player não informou — mantém o que tinha */ }
    }

    private static string Format(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        _timer.Stop();
        _session.MediaPropertiesChanged -= OnSessionEvent;
        _session.PlaybackInfoChanged -= OnSessionEvent;
        _session.TimelinePropertiesChanged -= OnSessionEvent;
    }
}
