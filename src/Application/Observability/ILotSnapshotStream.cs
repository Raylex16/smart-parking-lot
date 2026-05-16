namespace SmartParkingLot.Application.Observability;

using SmartParkingLot.Application.Queries;

public interface ILotSnapshotStream
{
    event EventHandler<LotSnapshotDto>? SnapshotChanged;
    LotSnapshotDto Current { get; }
}
