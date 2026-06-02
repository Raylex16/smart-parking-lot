namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// Responsable de logging y queries de acciones de dispositivos
/// </summary>
public interface IDeviceActionRepository
{
    Task<bool> LogDeviceActionAsync(string deviceId, string action, DateTime timestamp,
        CancellationToken ct = default);

    Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>>
        GetDeviceActionsAsync(string deviceId, CancellationToken ct = default);
}
