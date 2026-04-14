using SmartParkingLot.Domain;

namespace SmartParkingLot.Services;

// GRASP - Low Coupling: Interfaz que desacopla la lógica de capacidad de sus consumidores
public interface ICapacityService
{
    bool HasAvailableSpots();
    int GetAvailableCount();//
    ParkingSpot? ReserveSpot();
//    ParkingSpot? GetAvailableSpot();
    void ReleaseSpot(string spotId);
    void UpdateSpotState(SpotSensorReading reading);
}
    