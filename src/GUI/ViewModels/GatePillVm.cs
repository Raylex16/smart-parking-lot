using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Gui.Resources;

namespace SmartParkingLot.Gui.ViewModels;

public class GatePillVm
{
    public string GateId { get; init; } = "";
    public bool IsOpen { get; init; }
    public Brush PillBackground => IsOpen ? AppBrushes.SuccessBackground : AppBrushes.Layer2;
    public Brush PillBorderBrush => IsOpen ? AppBrushes.Success : AppBrushes.Stroke;
    public Brush TextBrush => IsOpen ? AppBrushes.Success : AppBrushes.Tx2;
    public string AccessibleLabel => IsOpen ? $"Puerta {GateId}: abierta" : $"Puerta {GateId}: cerrada";
}
