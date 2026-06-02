using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// Responsable de operaciones CRUD en ParkingSpots
/// </summary>
public interface ISpotRepository
{
    Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default);

    Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default);

    Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default);

    Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, string floor,
        CancellationToken ct = default);

    Task<int> RemoveOrphanSpotsAsync(string lotId, IEnumerable<string> validSpotIds,
        CancellationToken ct = default);
}
