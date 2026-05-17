using SmartParkingLot.Application.Logging;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Queries;

public sealed class LogQueryService : ILogQueryService
{
    private readonly IParkingRepository _repository;
    private readonly FileLogger _fileLogger;

    public LogQueryService(IParkingRepository repository, FileLogger fileLogger)
    {
        _repository = repository;
        _fileLogger = fileLogger;
    }

    public async Task<IReadOnlyList<RequestHistoryDto>> GetRequestHistoryAsync(
        string plate, CancellationToken ct = default)
    {
        var history = await _repository.GetRequestHistoryAsync(plate, ct);
        return history
            .Select(r => new RequestHistoryDto(r.RequestId, r.VehiclePlate, r.RequestType, r.Timestamp, r.Approved))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<SensorReadingDto>> GetSensorReadingsAsync(
        string sensorId, CancellationToken ct = default)
    {
        var readings = await _repository.GetSensorReadingsAsync(sensorId, ct);
        return readings
            .Select(r => new SensorReadingDto(r.Id, r.SensorId, r.Value, r.Timestamp))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<DeviceActionDto>> GetDeviceActionsAsync(
        string deviceId, CancellationToken ct = default)
    {
        var actions = await _repository.GetDeviceActionsAsync(deviceId, ct);
        return actions
            .Select(a => new DeviceActionDto(a.Id, a.DeviceId, a.Action, a.Timestamp))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<string> TailLogFile(int lines)
    {
        var path = _fileLogger.GetCurrentLogFilePath();
        if (!File.Exists(path))
            return Array.Empty<string>();

        var tail = new Queue<string>(lines);
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (tail.Count == lines) tail.Dequeue();
                tail.Enqueue(line);
            }
        }
        catch
        {
            return Array.Empty<string>();
        }

        return tail.ToList().AsReadOnly();
    }

    public string GetCurrentLogFilePath() => _fileLogger.GetCurrentLogFilePath();
}
