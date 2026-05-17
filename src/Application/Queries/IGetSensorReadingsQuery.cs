namespace SmartParkingLot.Application.Queries;

public interface IGetSensorReadingsQuery
{
    Task<IReadOnlyList<SensorReadingDto>> ExecuteAsync(string sensorId, CancellationToken ct = default);
}
