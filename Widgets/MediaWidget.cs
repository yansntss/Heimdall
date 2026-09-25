using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    // Segoe Fluent Icons (Win11) com fallback pra Segoe MDL2 Assets (Win10) — mesmos glifos nas duas.
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string GlyphPrevious = "";
    private const string GlyphNext = "";
    private const string GlyphPlay = "";
    private const string GlyphPause = "";

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private readonly Button _previous = CreateIconButton(GlyphPrevious);
    private readonly Button _playPause = CreateIconButton(GlyphPlay);
    private readonly Button _next = CreateIconButton(GlyphNext);

    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 4, 0)
    };

    private readonly StackPanel _root = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    public FrameworkElement View => _root;

    public MediaWidget()
    {
        _root.Children.Add(_previous);
        _root.Children.Add(_playPause);
        _root.Children.Add(_next);
        _root.Children.Add(_text);
    }

    public void ApplyOrientation(Orientation orientation) => _root.Orientation = orientation;

    public void Start()
    {
        _previous.Click += (_, _) => _ = _session?.TrySkipPreviousAsync();
        _playPause.Click += (_, _) => _ = _session?.TryTogglePlayPauseAsync();
        _next.Click += (_, _) => _ = _session?.TrySkipNextAsync();
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
        }

        _session = _manager?.GetCurrentSession();

        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnSessionEvent;
            _session.PlaybackInfoChanged += OnSessionEvent;
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

            _dispatcher.Invoke(() =>
            {
                if (string.IsNullOrWhiteSpace(full))
                {
                    _root.Visibility = Visibility.Collapsed;
                    return;
                }
                _text.Text = Truncate(full);
                _root.Visibility = Visibility.Visible;
            });
        }
        catch
        {
            _dispatcher.Invoke(() => _root.Visibility = Visibility.Collapsed);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLength ? value : value[..(MaxLength - 1)] + "…";

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

    public void Dispose()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionEvent;
            _session.PlaybackInfoChanged -= OnSessionEvent;
        }
    }
}
