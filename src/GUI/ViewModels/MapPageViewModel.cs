using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Gates;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Sensors;
using SmartParkingLot.Core;
using SmartParkingLot.Gui.Infrastructure;

namespace SmartParkingLot.Gui.ViewModels;

public partial class MapPageViewModel : ObservableObject
{
    private readonly ILotSnapshotStream _stream;
    private readonly IUiThreadDispatcher _ui;
    private readonly IManualSensorService _manualSensor;
    private readonly IGateOperationsService _gateOperations;

    [ObservableProperty] private string _mapTitle = "Planta 1";
    [ObservableProperty] private string _mapSubtitle = "";
    [ObservableProperty] private int _availableCount;
    [ObservableProperty] private int _occupiedCount;
    [ObservableProperty] private int _totalCount;

    public ObservableCollection<ZoneSpotGroupVm> ZoneGroups { get; } = new();
    public ObservableCollection<GateControlVm> GateControls { get; } = new();
    public ObservableCollection<GatePillVm> EntryGatePills { get; } = new();
    public ObservableCollection<GatePillVm> ExitGatePills { get; } = new();

    public MapPageViewModel(
        ILotSnapshotStream stream,
        IUiThreadDispatcher ui,
        IManualSensorService manualSensor,
        IGateOperationsService gateOperations)
    {
        _stream          = stream;
        _ui              = ui;
        _manualSensor    = manualSensor;
        _gateOperations  = gateOperations;
    }

    public void Activate()
    {
        MapTitle = $"Planta 1 — {_stream.Current.Name}";
        BuildZoneGroups(_stream.Current);
        BuildGateControls(_stream.Current);
        ApplySnapshot(_stream.Current);
        _stream.SnapshotChanged += OnSnapshotChanged;
    }

    public void Deactivate()
    {
        _stream.SnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(object? sender, LotSnapshotDto dto)
    {
        _ui.Enqueue(() => ApplySnapshot(dto));
    }

    private void ApplySnapshot(LotSnapshotDto dto)
    {
        TotalCount     = dto.TotalSpots;
        OccupiedCount  = dto.OccupiedSpots;
        AvailableCount = dto.TotalSpots - dto.OccupiedSpots;
        MapSubtitle    = $"{dto.OccupiedSpots}/{dto.TotalSpots} ocupados";

        foreach (var group in ZoneGroups)
            foreach (var tile in group.Spots)
            {
                var spotRow = dto.Spots.FirstOrDefault(s => s.Id == tile.SpotId);
                if (spotRow is not null)
                    tile.IsOccupied = spotRow.IsOccupied;
            }

        BuildGatePills(dto);
        foreach (var gc in GateControls)
        {
            var gateSummary = dto.Gates.FirstOrDefault(g => g.GateId == gc.GateId);
            gc.IsOpen = gateSummary?.IsOpen ?? false;
        }
    }

    private void BuildZoneGroups(LotSnapshotDto dto)
    {
        ZoneGroups.Clear();
        var zones = dto.Spots.GroupBy(s => ZoneOf(s.Id)).OrderBy(g => g.Key);
        foreach (var zone in zones)
        {
            var group = new ZoneSpotGroupVm { ZoneName = $"ZONA {zone.Key}" };
            foreach (var spot in zone)
            {
                var capturedId = spot.Id;
                var tile = new SpotTileVm
                {
                    SpotId        = spot.Id,
                    ShortTypeName = ShortType(spot.Type),
                    ToolTipText   = $"{spot.Id} — {spot.Address} ({spot.Type})",
                    IsOccupied    = spot.IsOccupied,
                    ToggleCommand = new RelayCommand(() => _ = ToggleSpotAsync(capturedId))
                };
                group.Spots.Add(tile);
            }
            ZoneGroups.Add(group);
        }
    }

    private void BuildGateControls(LotSnapshotDto dto)
    {
        GateControls.Clear();
        foreach (var gate in _gateOperations.GetRegisteredGates())
        {
            var gateSummary = dto.Gates.FirstOrDefault(g => g.GateId == gate.GateId);
            var isOpen = gateSummary?.IsOpen ?? false;
            var capturedGateId = gate.GateId;
            var vm = new GateControlVm
            {
                GateId    = gate.GateId,
                TypeLabel = gate.Type.ToString(),
                IsOpen    = isOpen
            };
            vm.ToggleCommand = new RelayCommand(() => _ = DoToggleGateAsync(vm, capturedGateId));
            GateControls.Add(vm);
        }
        BuildGatePills(dto);
    }

    private void BuildGatePills(LotSnapshotDto dto)
    {
        EntryGatePills.Clear();
        ExitGatePills.Clear();
        foreach (var gate in _gateOperations.GetRegisteredGates())
        {
            var gateSummary = dto.Gates.FirstOrDefault(g => g.GateId == gate.GateId);
            var isOpen = gateSummary?.IsOpen ?? false;
            var pill = new GatePillVm { GateId = gate.GateId, IsOpen = isOpen };
            if (gate.Type == GateType.ENTRY)
                EntryGatePills.Add(pill);
            else
                ExitGatePills.Add(pill);
        }
    }

    private async Task DoToggleGateAsync(GateControlVm vm, string gateId)
    {
        if (vm.IsOpen)
            await _gateOperations.CloseAsync(gateId);
        else
            await _gateOperations.OpenAsync(gateId);
        vm.IsOpen = !vm.IsOpen;
        BuildGatePills(_stream.Current);
    }

    private async Task ToggleSpotAsync(string spotId)
    {
        var current = _stream.Current.Spots.FirstOrDefault(s => s.Id == spotId);
        if (current is null) return;
        await _manualSensor.RecordSpotReadingAsync(spotId, !current.IsOccupied);
    }

    public async Task<(bool approved, int available)> RequestEntryAsync(string plate)
    {
        var result = await _gateOperations.RequestEntryAsync(plate, GuiConstants.ENTRY_GATE_ID);
        return (result.Approved, result.AvailableSpots);
    }

    public async Task<(bool approved, int available)> RequestExitAsync(string plate)
    {
        var result = await _gateOperations.RequestExitAsync(plate, GuiConstants.EXIT_GATE_ID);
        return (result.Approved, result.AvailableSpots);
    }

    [RelayCommand]
    public Task SimEntryIr() => _manualSensor.TriggerGateIrAsync(GuiConstants.ENTRY_GATE_ID);

    [RelayCommand]
    public Task SimExitIr() => _manualSensor.TriggerGateIrAsync(GuiConstants.EXIT_GATE_ID);

    private static string ZoneOf(string spotId)
    {
        var dash = spotId.IndexOf('-');
        return dash > 0 ? spotId[..dash] : spotId[..1];
    }

    private static string ShortType(string type) => type.ToLowerInvariant() switch
    {
        var t when t.Contains("est") => "STD",
        var t when t.Contains("pmr") || t.Contains("disc") => "PMR",
        var t when t.Contains("moto") => "MOTO",
        _ => type.Length > 4 ? type[..4].ToUpper() : type.ToUpper()
    };
}
