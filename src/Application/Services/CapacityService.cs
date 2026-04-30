using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

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
        var spots = _parkingLot.GetSpots();
        var spot = spots.FirstOrDefault(s => s.Id == reading.SpotId);

        if (spot is null)
        {
            Console.WriteLine($"[CapacityService] Espacio '{reading.SpotId}' no encontrado para actualizar");
            return;
        }

        spot.ApplyOccupancy(reading.IsOccupied, "sensor");
    }
}
