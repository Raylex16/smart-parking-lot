using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

public interface IParkingRepository
{
    Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default);

    Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default);

    Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default);

    Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default);

    Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default);

    Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, string floor,
        CancellationToken ct = default);

    Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType, string lotId,
        DateTime timestamp, bool approved, string? releasedSpotId = null, CancellationToken ct = default);

    Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>>
        GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default);

    Task<bool> LogSensorReadingAsync(string sensorId, string value, DateTime timestamp,
        CancellationToken ct = default);

    Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>>
        GetSensorReadingsAsync(string sensorId, CancellationToken ct = default);

    Task<bool> LogDeviceActionAsync(string deviceId, string action, DateTime timestamp,
        CancellationToken ct = default);

    Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>>
        GetDeviceActionsAsync(string deviceId, CancellationToken ct = default);

    Task<bool> LogAlertAsync(string alertId, string type, string message, DateTime timestamp,
        CancellationToken ct = default);
}
