using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core;

namespace SmartParkingLot.Gui.ViewModels;

public partial class HardwareConfigEditorViewModel : ObservableObject
{
    private readonly HardwareConfig _current;
    private readonly string _configPath;

    [ObservableProperty] private string _port = "";
    [ObservableProperty] private string _baudRate = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _needsRestart;

    public ObservableCollection<SpotMappingRowVm> Spots { get; } = new();
    public ObservableCollection<GateMappingRowVm> Gates { get; } = new();

    public HardwareConfigEditorViewModel(HardwareConfig current, string configPath)
    {
        _current    = current;
        _configPath = configPath;
        LoadFrom(current);
    }

    private void LoadFrom(HardwareConfig config)
    {
        Port     = config.Port;
        BaudRate = config.BaudRate.ToString();

        Spots.Clear();
        foreach (var s in config.Sensors)
            Spots.Add(NewSpotRow(new SpotMappingRowVm
            {
                SensorId = s.SensorId, SpotId = s.SpotId, ActuatorId = s.ActuatorId,
                Address = s.Address, Type = s.Type, Floor = s.Floor
            }));

        Gates.Clear();
        foreach (var g in config.Gates)
            Gates.Add(NewGateRow(new GateMappingRowVm
            {
                GateId = g.GateId, Type = g.Type.ToString(),
                IrSensorId = g.IrSensorId, ActuatorId = g.ActuatorId, Pin = g.Pin.ToString()
            }));
    }

    private SpotMappingRowVm NewSpotRow(SpotMappingRowVm row)
    {
        row.RemoveCommand = new RelayCommand(() => Spots.Remove(row));
        return row;
    }

    private GateMappingRowVm NewGateRow(GateMappingRowVm row)
    {
        row.RemoveCommand = new RelayCommand(() => Gates.Remove(row));
        return row;
    }

    [RelayCommand]
    private void AddSpot() => Spots.Add(NewSpotRow(new SpotMappingRowVm { SpotId = "NUEVO" }));

    [RelayCommand]
    private void AddGate() => Gates.Add(NewGateRow(new GateMappingRowVm { GateId = "G-NN", Type = "ENTRY" }));

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(BaudRate, out var baud))
        {
            StatusMessage = "BaudRate inválido: debe ser un número.";
            return;
        }

        var config = _current with
        {
            Port = Port.Trim(),
            BaudRate = baud,
            Sensors = Spots.Select(s => new SensorMapping(
                s.SensorId.Trim(), s.SpotId.Trim(), s.ActuatorId.Trim(),
                s.Address.Trim(), s.Type.Trim(), s.Floor.Trim())).ToList(),
            Gates = Gates.Select(g => new GateMapping(
                g.GateId.Trim(),
                Enum.TryParse<GateType>(g.Type, ignoreCase: true, out var t) ? t : GateType.ENTRY,
                g.IrSensorId.Trim(), g.ActuatorId.Trim(),
                int.TryParse(g.Pin, out var pin) ? pin : -1)).ToList()
        };

        var errors = HardwareConfigValidator.Validate(config);
        if (errors.Count > 0)
        {
            StatusMessage = "No se guardó. " + string.Join(" ", errors);
            return;
        }

        config.Save(_configPath);
        NeedsRestart  = true;
        StatusMessage = "Configuración guardada. Reinicia la aplicación para aplicar los cambios.";
    }
}
