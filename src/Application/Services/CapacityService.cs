using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Application;

// GRASP - Indirection: Servicio intermediario que desacopla al controlador de la lógica de capacidad
// GRASP - High Cohesion: Solo se encarga de operaciones relacionadas con la capacidad del parqueadero
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

    public void ReleaseSpot(string spotId)
    {
        // GRASP - Information Expert: Delega al ParkingLot buscar el spot, y al spot liberarse
        var spots = _parkingLot.GetSpots();
        var spot = spots.FirstOrDefault(s => s.Id == spotId);

        if (spot is not null)
        {
            spot.Release();
            Console.WriteLine($"[CapacityService] Espacio '{spotId}' liberado exitosamente");
        }
        else
        {
            Console.WriteLine($"[CapacityService] Espacio '{spotId}' no encontrado");
        }
    }

    public void UpdateSpotState(SpotSensorReading reading)
    {
        // GRASP - Information Expert: El servicio recibe la lectura del sensor y actualiza el spot correspondiente
        var spots = _parkingLot.GetSpots();
        var spot = spots.FirstOrDefault(s => s.Id == reading.SpotId);

        if (spot is null)
        {
            Console.WriteLine($"[CapacityService] Espacio '{reading.SpotId}' no encontrado para actualizar");
            return;
        }

        if (reading.IsOccupied && spot.IsAvailable())
        {
            spot.Occupy();
            Console.WriteLine($"[CapacityService] Espacio '{reading.SpotId}' marcado como OCUPADO por sensor");
        }
        else if (!reading.IsOccupied && !spot.IsAvailable())
        {
            spot.Release();
            Console.WriteLine($"[CapacityService] Espacio '{reading.SpotId}' marcado como LIBRE por sensor");
        }
    }
}
