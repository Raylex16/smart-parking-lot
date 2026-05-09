using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

public class CapacityService : ICapacityService
{
    private const string LogSource = "CapacityService";

    private readonly ParkingLot _parkingLot;
    private readonly ILogger _logger;

    public CapacityService(ParkingLot parkingLot, ILogger logger)
    {
        _parkingLot = parkingLot;
        _logger = logger;
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
            _logger.Info(LogSource, $"Espacio '{spotId}' liberado exitosamente");
        }
        else
        {
            _logger.Warn(LogSource, $"Espacio '{spotId}' no encontrado");
        }
    }

    public void UpdateSpotState(SpotSensorReading reading)
    {
        var spots = _parkingLot.GetSpots();
        var spot = spots.FirstOrDefault(s => s.Id == reading.SpotId);

        if (spot is null)
        {
            _logger.Warn(LogSource, $"Espacio '{reading.SpotId}' no encontrado para actualizar");
            return;
        }

        spot.ApplyOccupancy(reading.IsOccupied, "sensor");
    }
}
