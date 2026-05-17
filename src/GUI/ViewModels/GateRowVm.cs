namespace SmartParkingLot.Gui.ViewModels;

public class GateRowVm
{
    public string GateId { get; init; } = "";
    public string Type { get; init; } = "";
    public bool IsOpen { get; init; }
    public string StateLabel => IsOpen ? "Abierta" : "Cerrada";
    public string TypeLabel => Type;
}
