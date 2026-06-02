using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// Responsable de queries sobre Parking Lots
/// </summary>
public interface IParkingLotRepository
{
    Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default);

    Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default);

    Task<bool> UpdateLotModeAsync(string lotId, ParkingMode mode, CancellationToken ct = default);
}
