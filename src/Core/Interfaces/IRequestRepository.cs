namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// Responsable de logging y queries de requests de entrada/salida
/// </summary>
public interface IRequestRepository
{
    Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType, string lotId,
        DateTime timestamp, bool approved, string? releasedSpotId = null, CancellationToken ct = default);

    Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>>
        GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default);
}
