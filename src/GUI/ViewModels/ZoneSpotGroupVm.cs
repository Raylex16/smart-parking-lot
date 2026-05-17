using System.Collections.ObjectModel;

namespace SmartParkingLot.Gui.ViewModels;

public class ZoneSpotGroupVm
{
    public string ZoneName { get; init; } = "";
    public ObservableCollection<SpotTileVm> Spots { get; } = new();
}
