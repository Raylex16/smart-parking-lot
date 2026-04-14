using SmartParkingLot.Domain;

namespace SmartParkingLot.Services;

public interface ICapacityService
{
    bool HasAvailableSpots();

    int GetAvailableCount();

    ParkingSpot? ReserveSpot();
}
