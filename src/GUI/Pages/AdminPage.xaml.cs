using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class AdminPage : Page
{
    public AdminPageViewModel ViewModel { get; }

    public AdminPage(AdminPageViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Activate();
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Deactivate();

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.SearchText = sender.Text;
    }

    private void OnTypeFilterChanged(object sender, SelectionChangedEventArgs e)
        => ViewModel.TypeFilter = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    private void OnRefreshClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ViewModel.ReloadCommand.Execute(null);
}
