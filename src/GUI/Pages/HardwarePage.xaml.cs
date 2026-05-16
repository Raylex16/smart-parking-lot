using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class HardwarePage : Page
{
    public HardwarePageViewModel ViewModel { get; }

    public HardwarePage(HardwarePageViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;

        // Auto-scroll when new log lines arrive
        ViewModel.LogLines.CollectionChanged += (_, _) =>
        {
            if (ViewModel.AutoScroll)
                LogScroll.ChangeView(null, double.MaxValue, null, true);
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Activate();
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Deactivate();

    private void OnStateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            ViewModel.SelectedStateValue = item.Tag as string ?? "1";
    }
}
