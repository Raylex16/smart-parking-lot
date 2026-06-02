using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class SpotMappingRowVm : ObservableObject
{
    [ObservableProperty] private string _sensorId = "";
    [ObservableProperty] private string _spotId = "";
    [ObservableProperty] private string _actuatorId = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _type = "Estándar";
    [ObservableProperty] private string _floor = "Planta 1";

    public IRelayCommand? RemoveCommand { get; set; }
}
