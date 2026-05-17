using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Activate();
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Deactivate();
}
