namespace SmartParkingLot.Gui.ViewModels;

public class SpotAdminRowVm
{
    public string Id { get; init; } = "";
    public string Address { get; init; } = "";
    public string Type { get; init; } = "";
    public string Floor { get; init; } = "";
    public bool IsOccupied { get; init; }
    public string StateLabel => IsOccupied ? "Ocupado" : "Libre";
    public bool IsPmr => string.Equals(Type, "PMR", StringComparison.OrdinalIgnoreCase);
}
