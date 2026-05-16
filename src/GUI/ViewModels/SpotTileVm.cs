using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Gui.Resources;

namespace SmartParkingLot.Gui.ViewModels;

public partial class SpotTileVm : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TileBackground))]
    [NotifyPropertyChangedFor(nameof(TileBorderBrush))]
    [NotifyPropertyChangedFor(nameof(AccessibleLabel))]
    private bool _isOccupied;

    public string SpotId { get; init; } = "";
    public string ShortTypeName { get; init; } = "";
    public string ToolTipText { get; init; } = "";
    public IRelayCommand ToggleCommand { get; init; } = null!;

    public Brush TileBackground => IsOccupied
        ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentLightBrush"]
        : AppBrushes.Layer2;

    public Brush TileBorderBrush => IsOccupied
        ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"]
        : AppBrushes.Stroke;

    public string AccessibleLabel => IsOccupied
        ? $"Spot {SpotId}: ocupado"
        : $"Spot {SpotId}: libre";
}
