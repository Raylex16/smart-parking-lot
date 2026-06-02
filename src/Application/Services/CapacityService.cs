using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

/// <summary>
/// Servicio que gestiona la capacidad del estacionamiento.
/// Depende SOLO de interfaces segregadas que efectivamente usa.
/// </summary>
public class CapacityService : ICapacityService
{
    private const string LogSource = nameof(CapacityService);

    private readonly ParkingLot _parkingLot;
    private readonly ISpotRepository _spotRepository;
    private readonly ILogger _logger;

    public CapacityService(
        ParkingLot parkingLot,
        ISpotRepository spotRepository,
        ILogger logger)
    {
        _parkingLot = parkingLot;
        _spotRepository = spotRepository ?? throw new ArgumentNullException(nameof(spotRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
