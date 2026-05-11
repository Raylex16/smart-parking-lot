namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// Responsable de logging y queries de lecturas de sensores
/// </summary>
public interface ISensorRepository
{
    Task<bool> LogSensorReadingAsync(string sensorId, string value, DateTime timestamp,
        CancellationToken ct = default);

    Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>>
        GetSensorReadingsAsync(string sensorId, CancellationToken ct = default);
}
