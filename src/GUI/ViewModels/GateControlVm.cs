using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class GateControlVm : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    [NotifyPropertyChangedFor(nameof(ButtonLabel))]
    private bool _isOpen;

    public string GateId { get; init; } = "";
    public string TypeLabel { get; init; } = "";
    public string StateLabel => IsOpen ? "Abierta" : "Cerrada";
    public string ButtonLabel => IsOpen ? "Cerrar" : "Abrir";
    public IRelayCommand ToggleCommand { get; set; } = null!;
}
