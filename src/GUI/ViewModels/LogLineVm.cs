using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Gui.Resources;

namespace SmartParkingLot.Gui.ViewModels;

public class LogLineVm
{
    public string Text { get; init; } = "";
    public Brush Foreground { get; init; } = AppBrushes.Tx1;
}
