using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class GateMappingRowVm : ObservableObject
{
    [ObservableProperty] private string _gateId = "";
    [ObservableProperty] private string _type = "ENTRY";
    [ObservableProperty] private string _irSensorId = "";
    [ObservableProperty] private string _actuatorId = "";
    [ObservableProperty] private string _pin = "";

    public IRelayCommand? RemoveCommand { get; set; }
}
