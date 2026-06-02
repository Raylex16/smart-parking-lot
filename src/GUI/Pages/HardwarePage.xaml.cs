using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class HardwarePage : Page
{
    public HardwarePageViewModel ViewModel { get; }
    public HardwareConfigEditorViewModel Editor { get; }

    public HardwarePage(HardwarePageViewModel viewModel, HardwareConfigEditorViewModel editor)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Editor    = editor;
        Loaded   += (_, _) => ViewModel.Activate();
        Unloaded += (_, _) => ViewModel.Deactivate();

        ViewModel.LogLines.CollectionChanged += (_, _) =>
        {
            if (ViewModel.AutoScroll)
                LogScroll.ChangeView(null, double.MaxValue, null, true);
        };
    }

    private void OnStateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            ViewModel.SelectedStateValue = item.Tag as string ?? "1";
    }
}
