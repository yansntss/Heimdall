using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI.Popups;
using Heimdall.ViewModels;

namespace Heimdall.Widgets;

/// <summary>
/// Sino com badge numérico = lembretes pendentes hoje (<see cref="ReminderAgenda"/>).
/// Lê o mesmo AppConfig que o widget de lembretes altera, então basta reconsultar.
/// </summary>
public sealed class NotificationsWidget : IWidget
{
    private readonly AppConfig _cfg;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly TextBlock _count = WidgetUi.Text(9, "H.Brush.OnAccent", weight: FontWeights.Bold);
    private readonly Border _badge = new()
    {
        MinWidth = 14,
        Height = 14,
        CornerRadius = new CornerRadius(7),
        Padding = new Thickness(3, 0, 3, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 1, 1, 0),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private readonly FrameworkElement _root;
    private readonly Button? _button;

    public FrameworkElement View => _root;

    public NotificationsWidget(AppConfig cfg, bool isOverlay = false)
    {
        _cfg = cfg;

        _count.HorizontalAlignment = HorizontalAlignment.Center;
        _count.SetResourceReference(TextBlock.FontFamilyProperty, "H.Font.Mono");
        _badge.Child = _count;
        _badge.SetResourceReference(Border.BackgroundProperty, "H.Brush.Accent");

        // Badge por cima do botão inteiro (canto superior direito do 30x30), não só do ícone.
        var grid = new Grid();
        if (isOverlay)
        {
            var box = new Border { Child = WidgetUi.Icon("H.Icon.Bell") };
            box.SetResourceReference(FrameworkElement.WidthProperty, "H.Size.IconButton");
            box.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.IconButton");
            grid.Children.Add(box);
        }
        else
        {
            _button = WidgetUi.IconButton(WidgetUi.Icon("H.Icon.Bell"), Strings.NotificationsName);
            _button.Click += (_, _) => OpenReminders();
            grid.Children.Add(_button);
        }
        grid.Children.Add(_badge);
        // Botão pequeno (Compacta): badge menor e pra fora do canto, senão cobre o sino inteiro.
        grid.SizeChanged += (_, e) =>
        {
            bool small = e.NewSize.Height < 24;
            _badge.Height = small ? 11 : 14;
            _badge.MinWidth = small ? 11 : 14;
            _badge.CornerRadius = new CornerRadius(small ? 5.5 : 7);
            _badge.Margin = small ? new Thickness(0, -2, -5, 0) : new Thickness(0, 1, 1, 0);
            _count.FontSize = small ? 8 : 9;
        };
        _root = grid;

        _timer.Tick += (_, _) => Update();
    }

    public void ApplyOrientation(Orientation orientation) { }

    public void Start()
    {
        Update();
        _timer.Start();
        ReminderAgenda.Changed += Update;
    }

    private void Update()
    {
        int pending = ReminderAgenda.PendingCount(_cfg, DateTime.Now);
        _count.Text = pending > 9 ? "9+" : pending.ToString();
        _badge.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;

        string tooltip = Strings.NotificationsTooltip(pending);
        _root.ToolTip = tooltip;
        if (_button is not null)
        {
            _button.ToolTip = tooltip;
            AutomationProperties.SetHelpText(_button, tooltip);
        }
    }

    private void OpenReminders()
    {
        var anchor = _button!;
        FlyoutWindow.Toggle(anchor, _cfg.Edge,
            () => new RemindersFlyout(new RemindersFlyoutViewModel(_cfg, anchor)), Strings.FlyoutRemindersTitle);
    }

    public void Dispose()
    {
        _timer.Stop();
        ReminderAgenda.Changed -= Update;
    }
}
