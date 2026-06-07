using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class PlateRowVm : ObservableObject
{
    [ObservableProperty] private string _plate = "";

    public IRelayCommand<PlateRowVm>? RemoveCommand { get; set; }
}
