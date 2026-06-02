namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// Responsable de logging de alertas
/// </summary>
public interface IAlertRepository
{
    Task<bool> LogAlertAsync(string alertId, string type, string message, DateTime timestamp,
        CancellationToken ct = default);
}
