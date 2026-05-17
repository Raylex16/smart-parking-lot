namespace SmartParkingLot.Application.Queries;

public interface ILogQueryService
{
    Task<IReadOnlyList<RequestHistoryDto>> GetRequestHistoryAsync(string plate, CancellationToken ct = default);
    Task<IReadOnlyList<SensorReadingDto>> GetSensorReadingsAsync(string sensorId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceActionDto>> GetDeviceActionsAsync(string deviceId, CancellationToken ct = default);
    IReadOnlyList<string> TailLogFile(int lines);
    string GetCurrentLogFilePath();
}
