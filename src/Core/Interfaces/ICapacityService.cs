namespace SmartParkingLot.Core.Interfaces;

// GRASP - Low Coupling: Interfaz que desacopla la lógica de capacidad de sus consumidores
// SOLID - Dependency Inversion Principle: Los módulos de alto nivel (Request, Controller)
// dependen de esta abstracción, no de la implementación concreta (CapacityService).
public interface ICapacityService
{
    bool HasAvailableSpots();
    int GetAvailableCount();
    ParkingSpot? ReserveSpot();
    void ReleaseSpot(string spotId);
    void UpdateSpotState(SpotSensorReading reading);
}
