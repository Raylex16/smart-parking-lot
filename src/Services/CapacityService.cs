using SmartParkingLot.Domain;

namespace SmartParkingLot.Services;

public class CapacityService : ICapacityService
{
    private readonly ParkingLot _parkingLot;

    public CapacityService(ParkingLot parkingLot)
    {
        _parkingLot = parkingLot;
    }

    public bool HasAvailableSpots() => _parkingLot.IsAvailable();

    public int GetAvailableCount() => _parkingLot.AvailableSpots;

    public ParkingSpot? ReserveSpot()
    {
        var spot = _parkingLot.GetAvailableSpot();
        spot?.Occupy();
        return spot;
    }
}
