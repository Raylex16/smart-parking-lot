using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Infrastructure;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Gui.ViewModels;

public partial class MapPageViewModel : ObservableObject
{
    private readonly ILotSnapshotStream _stream;
    private readonly IUiThreadDispatcher _ui;
    private readonly IParkingRepository _repository;
    private readonly IEventPublisher _bus;
    private readonly GateController _gateController;
    private readonly ParkingLot _lot;
    private readonly IReadOnlyDictionary<string, Sensor<SpotSensorReading>> _spotSensors;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly HardwareConfig _config;

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
        IParkingRepository repository,
        IEventPublisher bus,
        GateController gateController,
        ParkingLot lot,
        IReadOnlyDictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        HardwareConfig config)
    {
        _stream         = stream;
        _ui             = ui;
        _repository     = repository;
        _bus            = bus;
        _gateController = gateController;
        _lot            = lot;
        _spotSensors    = spotSensors;
        _gateSensor     = gateSensor;
        _config         = config;
    }

    public void Activate()
    {
        MapTitle = $"Planta 1 — {_lot.Name}";
        BuildZoneGroups();
        BuildGateControls();
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

        // Sync IsOccupied from domain
        foreach (var group in ZoneGroups)
            foreach (var tile in group.Spots)
            {
                var domainSpot = _lot.GetSpots().FirstOrDefault(s => s.Id == tile.SpotId);
                if (domainSpot is not null)
                    tile.IsOccupied = domainSpot.IsOccupied;
            }

        // Refresh gate pills
        BuildGatePills();
        // Refresh gate control open/closed state
        foreach (var gc in GateControls)
        {
            var gate = _gateController.GetGateById(gc.GateId);
            gc.IsOpen = gate?.GetState() ?? false;
        }
    }

    private void BuildZoneGroups()
    {
        ZoneGroups.Clear();
        var spots = _lot.GetSpots();
        var zones = spots.GroupBy(s => ZoneOf(s.Id)).OrderBy(g => g.Key);
        foreach (var zone in zones)
        {
            var group = new ZoneSpotGroupVm { ZoneName = $"ZONA {zone.Key}" };
            foreach (var spot in zone)
            {
                var capturedSpot = spot; // closure capture
                var tile = new SpotTileVm
                {
                    SpotId        = spot.Id,
                    ShortTypeName = ShortType(spot.Type),
                    ToolTipText   = $"{spot.Id} — {spot.Address} ({spot.Type})",
                    IsOccupied    = spot.IsOccupied,
                    ToggleCommand = new RelayCommand(() => ToggleSpot(capturedSpot))
                };
                group.Spots.Add(tile);
            }
            ZoneGroups.Add(group);
        }
    }

    private void BuildGateControls()
    {
        GateControls.Clear();
        foreach (var cfg in _config.Gates)
        {
            var gate = _gateController.GetGateById(cfg.GateId);
            var open = gate?.GetState() ?? false;
            var capturedCfg  = cfg;
            var capturedGate = gate;
            var vm = new GateControlVm
            {
                GateId    = cfg.GateId,
                TypeLabel = cfg.Type.ToString(),
                IsOpen    = open
            };
            vm.ToggleCommand = new RelayCommand(() => DoToggleGate(vm, capturedCfg.GateId, capturedGate));
            GateControls.Add(vm);
        }
        BuildGatePills();
    }

    private void BuildGatePills()
    {
        EntryGatePills.Clear();
        ExitGatePills.Clear();
        foreach (var cfg in _config.Gates)
        {
            var gate = _gateController.GetGateById(cfg.GateId);
            var open = gate?.GetState() ?? false;
            var pill = new GatePillVm { GateId = cfg.GateId, IsOpen = open };
            if (cfg.Type == GateType.ENTRY)
                EntryGatePills.Add(pill);
            else
                ExitGatePills.Add(pill);
        }
    }

    private void DoToggleGate(GateControlVm vm, string gateId, IGate? gate)
    {
        if (gate is null) return;
        if (vm.IsOpen)
            gate.Close();
        else
            gate.Open();
        _ = _repository.LogDeviceActionAsync($"GATE-{gateId}",
            vm.IsOpen ? "CLOSE" : "OPEN", DateTime.Now);
        vm.IsOpen = !vm.IsOpen;
        BuildGatePills();
    }

    private void ToggleSpot(ParkingSpot spot)
    {
        var newState = !spot.IsOccupied;
        if (!_spotSensors.TryGetValue(spot.Id, out var sensor)) return;
        var reading = new SpotSensorReading(spot.Id, newState);
        sensor.CaptureReading(reading);
        var raw = newState ? "1" : "0";
        _ = _repository.LogSensorReadingAsync(sensor.Id, raw, DateTime.Now);
        _bus.Publish(new SensorReadingReceived(
            SensorId:   sensor.Id,
            SensorType: sensor.GetSensorType(),
            RawValue:   raw,
            Timestamp:  DateTimeOffset.Now));
    }

    public async Task<(bool approved, int available)> RequestEntryAsync(string plate)
    {
        var occupiedBefore = _lot.GetSpots()
            .Where(s => s.IsOccupied).Select(s => s.Id).ToHashSet();

        var gateReading = new GateSensorReading(plate, GuiConstants.ENTRY_GATE_ID);
        _gateSensor.CaptureReading(gateReading);
        await _repository.LogSensorReadingAsync(_gateSensor.Id, $"plate:{plate}", DateTime.Now);

        var request = new EntryRequest(plate) { GateId = GuiConstants.ENTRY_GATE_ID };
        await _gateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "ENTRY", _lot.Id,
            request.Timestamp, request.Approved);

        if (request.Approved)
        {
            var assigned = _lot.GetSpots()
                .FirstOrDefault(s => s.IsOccupied && !occupiedBefore.Contains(s.Id));
            if (assigned is not null)
                await _repository.UpdateSpotStatusAsync(assigned.Id, true);
            await _repository.LogDeviceActionAsync($"GATE-{GuiConstants.ENTRY_GATE_ID}", "OPEN", DateTime.Now);
        }

        return (request.Approved, _lot.AvailableSpots);
    }

    public async Task<(bool approved, int available)> RequestExitAsync(string plate)
    {
        var request = new ExitRequest(plate) { GateId = GuiConstants.EXIT_GATE_ID };
        await _gateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "EXIT", _lot.Id,
            request.Timestamp, approved: true);
        await _repository.LogDeviceActionAsync($"GATE-{GuiConstants.EXIT_GATE_ID}", "OPEN", DateTime.Now);

        return (true, _lot.AvailableSpots);
    }

    [RelayCommand]
    public void SimEntryIr() =>
        _bus.Publish(new SensorReadingReceived("GATE-IR1", "IR", "1", DateTimeOffset.Now));

    [RelayCommand]
    public void SimExitIr() =>
        _bus.Publish(new SensorReadingReceived("GATE-IR2", "IR", "1", DateTimeOffset.Now));

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
