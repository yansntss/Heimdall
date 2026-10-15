using System.Windows.Controls;
using Heimdall.ViewModels;

namespace Heimdall.UI.Popups;

public partial class RemindersFlyout : UserControl
{
    internal RemindersFlyout(RemindersFlyoutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
