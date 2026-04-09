using SmartParkingLot.Domain;

namespace SmartParkingLot.Services;

/// <summary>
/// Implementa la lógica de negocio relacionada con la capacidad del parqueadero.
/// </summary>
/// <remarks>
/// GRASP - Information Expert:
/// CapacityService coordina la consulta de disponibilidad delegando en ParkingLot,
/// que es quien tiene la información real de los espacios. CapacityService no
/// duplica esa información; simplemente la expone a través de un contrato de servicio.
///
/// GRASP - Low Coupling:
/// CapacityService solo conoce ParkingLot (dominio puro). No sabe nada de puertas,
/// sensores físicos ni controladores; ese aislamiento limita el impacto de los cambios.
/// </remarks>
public class CapacityService : ICapacityService
{
    // GRASP - Information Expert: ParkingLot es la fuente de verdad de los espacios.
    private readonly ParkingLot _parkingLot;

    public CapacityService(ParkingLot parkingLot)
    {
        _parkingLot = parkingLot;
    }

    // GRASP - Information Expert: delega en ParkingLot, que es el verdadero experto.
    public bool HasAvailableSpots() => _parkingLot.IsAvailable();

    public int GetAvailableCount() => _parkingLot.AvailableSpots;

    /// <summary>
    /// Busca y reserva atómicamente el primer espacio disponible.
    /// </summary>
    public ParkingSpot? ReserveSpot()
    {
        // GRASP - Information Expert: ParkingLot sabe cuál espacio está libre.
        var spot = _parkingLot.GetAvailableSpot();
        spot?.Occupy(); // ParkingSpot sabe cómo ocuparse a sí mismo.
        return spot;
    }
}
