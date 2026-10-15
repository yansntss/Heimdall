using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Services;
using Heimdall.UI.Controls;

namespace Heimdall.Widgets;

/// <summary>
/// Estado da rede: Wi-Fi, cabo ou sem conexão — pela interface que tem gateway padrão.
/// Atualiza nos eventos de NetworkChange (sem polling). Clique abre as configurações de
/// rede do Windows.
/// </summary>
public sealed class NetworkWidget : IWidget
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Icon _icon = WidgetUi.Icon("H.Icon.Wifi");
    private readonly FrameworkElement _root;

    public FrameworkElement View => _root;

    public NetworkWidget(bool isOverlay = false)
    {
        if (isOverlay)
        {
            var box = new Border { Child = _icon };
            box.SetResourceReference(FrameworkElement.WidthProperty, "H.Size.IconButton");
            box.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.IconButton");
            _root = box;
        }
        else
        {
            var button = WidgetUi.IconButton(_icon, Strings.NetworkName);
            button.Click += (_, _) => OpenNetworkSettings();
            _root = button;
        }
    }

    public void ApplyOrientation(Orientation orientation) { }

    public void Start()
    {
        Update();
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(new Action(Update));

    private void Update()
    {
        var active = FindActiveInterface();
        string tooltip;
        if (active is null)
        {
            _icon.Geometry = WidgetUi.Geometry("H.Icon.WifiOff");
            tooltip = Strings.NetworkDisconnected;
        }
        else
        {
            bool wifi = active.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
            _icon.Geometry = WidgetUi.Geometry(wifi ? "H.Icon.Wifi" : "H.Icon.Ethernet");
            tooltip = Strings.NetworkConnected(active.Name, wifi);
        }

        _root.ToolTip = tooltip;
        AutomationProperties.SetHelpText(_root, tooltip);
    }

    /// <summary>Interface ativa com gateway IPv4/IPv6 — ignora loopback, túneis e adaptadores virtuais sem rota.</summary>
    private static NetworkInterface? FindActiveInterface()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Where(n => n.GetIPProperties().GatewayAddresses.Any(g =>
                    g.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6
                    && !g.Address.Equals(System.Net.IPAddress.Any) && !g.Address.Equals(System.Net.IPAddress.IPv6Any)))
                // Wi-Fi e cabo antes de adaptadores genéricos (VPN, virtual) quando houver mais de um.
                .OrderBy(n => n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet ? 0 : 1)
                .FirstOrDefault();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static void OpenNetworkSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true }); }
        catch { /* sem o app Configurações: nada a fazer */ }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }
}
