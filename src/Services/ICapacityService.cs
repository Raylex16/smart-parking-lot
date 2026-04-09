using SmartParkingLot.Domain;

namespace SmartParkingLot.Services;

/// <summary>
/// Contrato para el servicio que consulta y gestiona la capacidad del parqueadero.
/// </summary>
/// <remarks>
/// GRASP - Low Coupling:
/// GateController depende de esta interfaz, no de la implementación concreta.
/// Esto permite sustituir la lógica de capacidad (p. ej. capacidad remota vía IoT)
/// sin tocar el controlador.
/// </remarks>
public interface ICapacityService
{
    /// <summary>Retorna true si existe al menos un espacio libre.</summary>
    bool HasAvailableSpots();

    /// <summary>Número actual de espacios disponibles.</summary>
    int GetAvailableCount();

    /// <summary>
    /// Reserva y retorna el primer espacio libre, o null si no hay ninguno.
    /// </summary>
    ParkingSpot? ReserveSpot();
}
