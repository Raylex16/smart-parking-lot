using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class LogPage : Page
{
    public LogPageViewModel ViewModel { get; }

    public LogPage(LogPageViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Activate();
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Deactivate();

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedTypeIndex = TypeCombo.SelectedIndex;
        QueryBox.PlaceholderText = TypeCombo.SelectedIndex switch
        {
            0 => "placa (ej: ABC-123)",
            1 => "sensorId (ej: SEN-SPOT-A-01)",
            2 => "deviceId (ej: GATE-G-01)",
            _ => ""
        };
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.QueryText = sender.Text;
        ViewModel.LoadCommand.Execute(null);
    }

    private void OnLoadClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.QueryText = QueryBox.Text ?? "";
        ViewModel.LoadCommand.Execute(null);
    }
}
