using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.Media.Control;

namespace Heimdall.Widgets;

/// <summary>
/// Controle de mídia via SMTC (Spotify/YouTube/qualquer player) — API nativa do
/// Windows, sem login nem API key do serviço.
/// </summary>
public sealed class MediaWidget : IWidget
{
    private const int MaxLength = 40;

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Cursor = Cursors.Hand,
        Visibility = Visibility.Collapsed
    };

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    public FrameworkElement View => _text;

    public void ApplyOrientation(Orientation orientation)
    {
        // Texto único; nada a ajustar por orientação.
    }

    public void Start()
    {
        _text.MouseDown += OnMouseDown;
        _ = InitializeAsync();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) TogglePlayPause();
        else if (e.ChangedButton == MouseButton.Middle) SkipNext();
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
        }

        _session = _manager?.GetCurrentSession();

        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnSessionEvent;
            _session.PlaybackInfoChanged += OnSessionEvent;
        }

        _ = RefreshAsync();
    }

    private void OnSessionEvent(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _dispatcher.BeginInvoke(new Action(() => _ = RefreshAsync()));

    private async Task RefreshAsync()
    {
        var session = _session;
        if (session is null)
        {
            _dispatcher.Invoke(() => _text.Visibility = Visibility.Collapsed);
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            string title = props.Title ?? "";
            string artist = props.Artist ?? "";
            string full = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} — {title}";

            _dispatcher.Invoke(() =>
            {
                if (string.IsNullOrWhiteSpace(full))
                {
                    _text.Visibility = Visibility.Collapsed;
                    return;
                }
                _text.Text = Truncate(full);
                _text.Visibility = Visibility.Visible;
            });
        }
        catch
        {
            _dispatcher.Invoke(() => _text.Visibility = Visibility.Collapsed);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLength ? value : value[..(MaxLength - 1)] + "…";

    private void TogglePlayPause()
    {
        var session = _session;
        if (session is null) return;

        var status = session.GetPlaybackInfo()?.PlaybackStatus;
        _ = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            ? session.TryPauseAsync()
            : session.TryPlayAsync();
    }

    private void SkipNext() => _ = _session?.TrySkipNextAsync();

    public void Dispose()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionEvent;
            _session.PlaybackInfoChanged -= OnSessionEvent;
        }
    }
}
