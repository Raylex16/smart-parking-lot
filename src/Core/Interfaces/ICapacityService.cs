namespace SmartParkingLot.Core.Interfaces;

public interface ICapacityService
{
    bool HasAvailableSpots();
    int GetAvailableCount();
    ParkingSpot? ReserveSpot();
    void ReleaseSpot(string spotId);
    void UpdateSpotState(SpotSensorReading reading);
}
