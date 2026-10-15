using System.Windows.Controls;
using Heimdall.ViewModels;

namespace Heimdall.UI.Popups;

public partial class SystemFlyout : UserControl
{
    internal SystemFlyout(SystemFlyoutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
