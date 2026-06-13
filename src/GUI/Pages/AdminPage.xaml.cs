using Microsoft.UI.Xaml.Controls;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class AdminPage : Page
{
    public AccessControlViewModel ViewModel { get; }

    public AdminPage(AccessControlViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Loaded   += (_, _) => ViewModel.Activate();
        Unloaded += (_, _) => ViewModel.Deactivate();
    }
}
