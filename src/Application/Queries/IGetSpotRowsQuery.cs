namespace SmartParkingLot.Application.Queries;

public interface IGetSpotRowsQuery
{
    Task<IReadOnlyList<SpotRowDto>> ExecuteAsync(Guid lotId, CancellationToken ct = default);
}
