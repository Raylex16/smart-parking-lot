using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Queries;

public sealed class GetSpotRowsQuery : IGetSpotRowsQuery
{
    private readonly IParkingRepository _repository;

    public GetSpotRowsQuery(IParkingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SpotRowDto>> ExecuteAsync(Guid lotId, CancellationToken ct = default)
    {
        var spots = await _repository.GetSpotsByLotIdAsync(lotId.ToString(), ct);

        return spots
            .Select(s => new SpotRowDto(
                Id: s.Id,
                Zone: s.Address,   // Address encodes the zone/location identifier
                Type: s.Type,
                Address: s.Address,
                IsOccupied: s.IsOccupied,
                Floor: int.TryParse(s.Floor, out var f) ? f : 0))
            .ToList();
    }
}
