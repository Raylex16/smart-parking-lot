using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// SOLID - Dependency Inversion Principle (DIP):
/// Abstracción que desacopla la lógica de negocio de la persistencia.
/// Los consumidores dependen de esta interfaz, no de SqliteParkingRepository.
///
/// SOLID - Interface Segregation Principle (ISP):
/// Interface segregada con responsabilidades claras sobre parking lots y spots.
/// </summary>
public interface IParkingRepository
{
    // ── Operaciones sobre ParkingLot ──

    /// <summary>Obtiene un lote de estacionamiento por su ID.</summary>
    Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default);

    // ── Operaciones sobre ParkingSpot ──

    /// <summary>Obtiene todos los espacios de un lote específico.</summary>
    Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default);

    /// <summary>Obtiene los espacios disponibles de un lote.</summary>
    Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default);

    /// <summary>Obtiene los espacios ocupados de un lote.</summary>
    Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default);

    /// <summary>Actualiza el estado (disponibilidad y ocupación) de un espacio.</summary>
    Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default);

    /// <summary>
    /// Inserta el espacio si no existe (idempotente). Permite que hardware.json
    /// sea la fuente de verdad: agregar un sensor registra el spot automáticamente.
    /// </summary>
    Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, string floor,
        CancellationToken ct = default);

    // ── Operaciones de auditoría ──

    /// <summary>Registra un Request (entrada o salida) para auditoría.</summary>
    Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType, string lotId,
        DateTime timestamp, bool approved, string? releasedSpotId = null, CancellationToken ct = default);

    /// <summary>Obtiene el historial de requests de un vehículo.</summary>
    Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>>
        GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default);

    // ── Lecturas de Sensores ──

    /// <summary>Registra una lectura de sensor con timestamp.</summary>
    Task<bool> LogSensorReadingAsync(string sensorId, string value, DateTime timestamp,
        CancellationToken ct = default);

    /// <summary>Obtiene el historial de lecturas de un sensor.</summary>
    Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>>
        GetSensorReadingsAsync(string sensorId, CancellationToken ct = default);

    // ── Acciones de Dispositivos ──

    /// <summary>Registra una acción de dispositivo (LED ON/OFF, GATE, etc.) con timestamp.</summary>
    Task<bool> LogDeviceActionAsync(string deviceId, string action, DateTime timestamp,
        CancellationToken ct = default);

    /// <summary>Obtiene el historial de acciones de un dispositivo.</summary>
    Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>>
        GetDeviceActionsAsync(string deviceId, CancellationToken ct = default);

    // ── Alertas ──

    /// <summary>Registra una alerta con su tipo, mensaje y timestamp.</summary>
    Task<bool> LogAlertAsync(string alertId, string type, string message, DateTime timestamp,
        CancellationToken ct = default);
}
