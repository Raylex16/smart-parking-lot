namespace SmartParkingLot.Gui.ViewModels;

public class ZoneRowVm
{
    public string Zone { get; init; } = "";
    public int Occupied { get; init; }
    public int Total { get; init; }
    public int OccupancyPct => Total == 0 ? 0 : (int)Math.Round(100.0 * Occupied / Total);
    public string Summary => $"{Occupied}/{Total}";
}
