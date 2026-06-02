using Microsoft.UI.Xaml.Controls;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class AdminPage : Page
{
    public AdminPageViewModel ViewModel { get; }

    public AdminPage(AdminPageViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Loaded   += (_, _) => ViewModel.Activate();
        Unloaded += (_, _) => ViewModel.Deactivate();
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.SearchText = sender.Text;
    }

    private void OnTypeFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.TypeFilter = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
    }

    private void OnRefreshClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ViewModel.ReloadCommand.Execute(null);
}
