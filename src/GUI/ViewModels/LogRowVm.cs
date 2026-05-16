using Microsoft.UI.Xaml.Media;

namespace SmartParkingLot.Gui.ViewModels;

public class LogRowVm
{
    public string Timestamp { get; init; } = "";
    public string TypeLabel { get; init; } = "";
    public string Detail    { get; init; } = "";
    public string Reference { get; init; } = "";
    public string BadgeKind { get; init; } = "Neutral"; // "Success","Danger","Accent","Neutral"

    public Brush TypeBackground => BadgeKind switch
    {
        "Success" => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SuccessBackground"],
        "Danger"  => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["DangerBackground"],
        "Accent"  => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentLightBrush"],
        _         => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NeutralBackground"]
    };

    public Brush TypeForeground => BadgeKind switch
    {
        "Success" => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SuccessBrush"],
        "Danger"  => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["DangerBrush"],
        "Accent"  => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"],
        _         => (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NeutralBrush"]
    };
}
