using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Queries;

public sealed class GetLotSnapshotQuery : IGetLotSnapshotQuery
{
    private readonly IParkingRepository _repository;
    private readonly GateController _gateController;
    private readonly GateTypeRegistry _gateTypes;
    private readonly string _lotId;

    public GetLotSnapshotQuery(
        IParkingRepository repository,
        GateController gateController,
        GateTypeRegistry gateTypes,
        string lotId)
    {
        _repository = repository;
        _gateController = gateController;
        _gateTypes = gateTypes;
        _lotId = lotId;
    }

    public async Task<LotSnapshotDto> ExecuteAsync(CancellationToken ct = default)
    {
        var lot = await _repository.GetParkingLotByIdAsync(_lotId, ct)
            ?? throw new InvalidOperationException($"Parqueadero '{_lotId}' no encontrado.");

        var spots = (await _repository.GetSpotsByLotIdAsync(_lotId, ct)).ToList();

        // Zone summaries: group by Address (used as zone identifier)
        var zoneSummaries = spots
            .GroupBy(s => s.Address)
            .Select(g => new ZoneSummaryDto(
                Zone: g.Key,
                Occupied: g.Count(s => s.IsOccupied),
                Total: g.Count()))
            .ToList();

        // Gates: iterate registered gates in GateController
        var gates = _gateController.GetRegisteredGates()
            .Select(kvp => new GateSummaryDto(
                GateId: kvp.Key,
                Type: _gateTypes.GetType(kvp.Key),
                IsOpen: kvp.Value.GetState()))
            .ToList();

        // Parse lot.Id as Guid; fall back to Guid.Empty if not a valid Guid
        var lotGuid = Guid.TryParse(lot.Id, out var parsed) ? parsed : Guid.Empty;

        return new LotSnapshotDto(
            Id: lotGuid,
            Name: lot.Name,
            TotalSpots: spots.Count,
            OccupiedSpots: spots.Count(s => s.IsOccupied),
            ZoneSummaries: zoneSummaries,
            Gates: gates);
    }
}
