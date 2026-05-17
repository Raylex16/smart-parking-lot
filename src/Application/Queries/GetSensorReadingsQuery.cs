using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Queries;

public sealed class GetSensorReadingsQuery : IGetSensorReadingsQuery
{
    private readonly IParkingRepository _repository;

    public GetSensorReadingsQuery(IParkingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SensorReadingDto>> ExecuteAsync(
        string sensorId, CancellationToken ct = default)
    {
        var readings = await _repository.GetSensorReadingsAsync(sensorId, ct);
        return readings
            .Select(r => new SensorReadingDto(r.Id, r.SensorId, r.Value, r.Timestamp))
            .ToList()
            .AsReadOnly();
    }
}
