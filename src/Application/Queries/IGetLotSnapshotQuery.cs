namespace SmartParkingLot.Application.Queries;

public interface IGetLotSnapshotQuery
{
    Task<LotSnapshotDto> ExecuteAsync(CancellationToken ct = default);
}
