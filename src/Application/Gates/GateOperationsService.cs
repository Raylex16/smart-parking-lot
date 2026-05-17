using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Application.Gates;

public sealed class GateOperationsService : IGateOperationsService
{
    private readonly GateController _gateController;
    private readonly IParkingRepository _repository;
    private readonly ParkingLot _lot;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly IReadOnlyDictionary<string, GateType> _gateTypes;

    public GateOperationsService(
        GateController gateController,
        IParkingRepository repository,
        ParkingLot lot,
        Sensor<GateSensorReading> gateSensor,
        IReadOnlyDictionary<string, GateType> gateTypes)
    {
        _gateController = gateController;
        _repository     = repository;
        _lot            = lot;
        _gateSensor     = gateSensor;
        _gateTypes      = gateTypes;
    }

    public IReadOnlyList<GateInfoDto> GetRegisteredGates() =>
        _gateController.GetRegisteredGates()
            .Select(kvp =>
            {
                var type = _gateTypes.TryGetValue(kvp.Key, out var t) ? t : GateType.ENTRY;
                return new GateInfoDto(kvp.Key, type, kvp.Value.GetState());
            })
            .ToList()
            .AsReadOnly();

    public async Task<GateOperationResultDto> RequestEntryAsync(
        string plate, string gateId, CancellationToken ct = default)
    {
        var occupiedBefore = _lot.GetSpots()
            .Where(s => s.IsOccupied).Select(s => s.Id).ToHashSet();

        var gateReading = new GateSensorReading(plate, gateId);
        _gateSensor.CaptureReading(gateReading);
        await _repository.LogSensorReadingAsync(_gateSensor.Id, $"plate:{plate}", DateTime.Now, ct);

        var request = new EntryRequest(plate) { GateId = gateId };
        await _gateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "ENTRY", _lot.Id,
            request.Timestamp, request.Approved, ct: ct);

        if (request.Approved)
        {
            var assigned = _lot.GetSpots()
                .FirstOrDefault(s => s.IsOccupied && !occupiedBefore.Contains(s.Id));
            if (assigned is not null)
                await _repository.UpdateSpotStatusAsync(assigned.Id, true, ct);
            await _repository.LogDeviceActionAsync($"GATE-{gateId}", "OPEN", DateTime.Now, ct);
        }

        return new GateOperationResultDto(request.Approved, _lot.AvailableSpots);
    }

    public async Task<GateOperationResultDto> RequestExitAsync(
        string plate, string gateId, CancellationToken ct = default)
    {
        var request = new ExitRequest(plate) { GateId = gateId };
        await _gateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "EXIT", _lot.Id,
            request.Timestamp, approved: true, ct: ct);
        await _repository.LogDeviceActionAsync($"GATE-{gateId}", "OPEN", DateTime.Now, ct);

        return new GateOperationResultDto(Approved: true, _lot.AvailableSpots);
    }

    public Task OpenAsync(string gateId, CancellationToken ct = default)
    {
        var gate = _gateController.GetGateById(gateId)
            ?? throw new InvalidOperationException($"Puerta '{gateId}' no registrada.");
        gate.Open();
        return _repository.LogDeviceActionAsync($"GATE-{gateId}", "OPEN", DateTime.Now, ct);
    }

    public Task CloseAsync(string gateId, CancellationToken ct = default)
    {
        var gate = _gateController.GetGateById(gateId)
            ?? throw new InvalidOperationException($"Puerta '{gateId}' no registrada.");
        gate.Close();
        return _repository.LogDeviceActionAsync($"GATE-{gateId}", "CLOSE", DateTime.Now, ct);
    }
}
