using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Infrastructure.Repositories;

/// <summary>
/// Composite repository que implementa IParkingRepository
/// delegando a los repositorios segregados.
/// 
/// Propósito: Mantener compatibilidad hacia atrás durante migración
/// a interfaces segregadas.
/// 
/// NOTA: Para código nuevo, usa los repositorios segregados directamente.
/// Este composite solo existe para soporte a código heredado.
/// </summary>
[Obsolete("Use segregated repositories instead")]
public class CompositeParkingRepository : IParkingRepository
{
    private readonly IParkingLotRepository _parkingLotRepository;
    private readonly ISpotRepository _spotRepository;
    private readonly IRequestRepository _requestRepository;
    private readonly ISensorRepository _sensorRepository;
    private readonly IDeviceActionRepository _deviceActionRepository;
    private readonly IAlertRepository _alertRepository;

    public CompositeParkingRepository(
        IParkingLotRepository parkingLotRepository,
        ISpotRepository spotRepository,
        IRequestRepository requestRepository,
        ISensorRepository sensorRepository,
        IDeviceActionRepository deviceActionRepository,
        IAlertRepository alertRepository)
    {
        _parkingLotRepository = parkingLotRepository ?? throw new ArgumentNullException(nameof(parkingLotRepository));
        _spotRepository = spotRepository ?? throw new ArgumentNullException(nameof(spotRepository));
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _sensorRepository = sensorRepository ?? throw new ArgumentNullException(nameof(sensorRepository));
        _deviceActionRepository = deviceActionRepository ?? throw new ArgumentNullException(nameof(deviceActionRepository));
        _alertRepository = alertRepository ?? throw new ArgumentNullException(nameof(alertRepository));
    }

    // IParkingLotRepository delegation
    public Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default)
        => _parkingLotRepository.GetParkingLotByIdAsync(lotId, ct);

    public Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default)
        => _parkingLotRepository.GetSpotsByLotIdAsync(lotId, ct);

    public Task<bool> UpdateLotModeAsync(string lotId, ParkingMode mode, CancellationToken ct = default)
        => _parkingLotRepository.UpdateLotModeAsync(lotId, mode, ct);

    // ISpotRepository delegation
    public Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default)
        => _spotRepository.GetAvailableSpotsAsync(lotId, ct);

    public Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default)
        => _spotRepository.GetOccupiedSpotsAsync(lotId, ct);

    public Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default)
        => _spotRepository.UpdateSpotStatusAsync(spotId, isOccupied, ct);

    public Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, string floor, CancellationToken ct = default)
        => _spotRepository.EnsureSpotExistsAsync(spotId, lotId, address, type, floor, ct);

    public Task<int> RemoveOrphanSpotsAsync(string lotId, IEnumerable<string> validSpotIds, CancellationToken ct = default)
        => _spotRepository.RemoveOrphanSpotsAsync(lotId, validSpotIds, ct);

    // IRequestRepository delegation
    public Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType, string lotId, DateTime timestamp, bool approved, string? releasedSpotId = null, CancellationToken ct = default)
        => _requestRepository.LogRequestAsync(requestId, vehiclePlate, requestType, lotId, timestamp, approved, releasedSpotId, ct);

    public Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>> GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default)
        => _requestRepository.GetRequestHistoryAsync(vehiclePlate, ct);

    // ISensorRepository delegation
    public Task<bool> LogSensorReadingAsync(string sensorId, string value, DateTime timestamp, CancellationToken ct = default)
        => _sensorRepository.LogSensorReadingAsync(sensorId, value, timestamp, ct);

    public Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>> GetSensorReadingsAsync(string sensorId, CancellationToken ct = default)
        => _sensorRepository.GetSensorReadingsAsync(sensorId, ct);

    // IDeviceActionRepository delegation
    public Task<bool> LogDeviceActionAsync(string deviceId, string action, DateTime timestamp, CancellationToken ct = default)
        => _deviceActionRepository.LogDeviceActionAsync(deviceId, action, timestamp, ct);

    public Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>> GetDeviceActionsAsync(string deviceId, CancellationToken ct = default)
        => _deviceActionRepository.GetDeviceActionsAsync(deviceId, ct);

    // IAlertRepository delegation
    public Task<bool> LogAlertAsync(string alertId, string type, string message, DateTime timestamp, CancellationToken ct = default)
        => _alertRepository.LogAlertAsync(alertId, type, message, timestamp, ct);
}
