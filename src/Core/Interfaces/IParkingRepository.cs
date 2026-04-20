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

    /// <summary>Obtiene todos los lotes de estacionamiento.</summary>
    Task<IEnumerable<ParkingLot>> GetAllParkingLotsAsync(CancellationToken ct = default);

    /// <summary>Crea un nuevo lote de estacionamiento en la BD.</summary>
    Task<bool> AddParkingLotAsync(ParkingLot lot, CancellationToken ct = default);

    /// <summary>Actualiza un lote de estacionamiento existente.</summary>
    Task<bool> UpdateParkingLotAsync(ParkingLot lot, CancellationToken ct = default);

    /// <summary>Elimina un lote de estacionamiento por su ID.</summary>
    Task<bool> DeleteParkingLotAsync(string lotId, CancellationToken ct = default);

    // ── Operaciones sobre ParkingSpot ──

    /// <summary>Obtiene un espacio de estacionamiento por su ID.</summary>
    Task<ParkingSpot?> GetParkingSpotByIdAsync(string spotId, CancellationToken ct = default);

    /// <summary>Obtiene todos los espacios de un lote específico.</summary>
    Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default);

    /// <summary>Obtiene los espacios disponibles de un lote.</summary>
    Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default);

    /// <summary>Obtiene los espacios ocupados de un lote.</summary>
    Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default);

    /// <summary>Agregar un nuevo espacio de estacionamiento.</summary>
    Task<bool> AddParkingSpotAsync(string lotId, ParkingSpot spot, CancellationToken ct = default);

    /// <summary>Actualiza el estado (disponibilidad y ocupación) de un espacio.</summary>
    Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default);

    /// <summary>Elimina un espacio de estacionamiento.</summary>
    Task<bool> DeleteParkingSpotAsync(string spotId, CancellationToken ct = default);

    // ── Operaciones sobre auditoría ──

    /// <summary>Registra un Request (entrada o salida) para auditoría, incluido spot liberado en EXIT.</summary>
    Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType, string lotId, 
        DateTime timestamp, bool approved, string? releasedSpotId = null, CancellationToken ct = default);

    /// <summary>Obtiene el historial de requests de un vehículo.</summary>
    Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>> 
        GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default);

    // ── Lecturas de Sensores (Rúbrica: "Debe guardar lecturas del sensor: valor, timestamp") ──

    /// <summary>Registra una lectura de sensor con timestamp (rúbrica).</summary>
    Task<bool> LogSensorReadingAsync(string sensorId, string value, DateTime timestamp, 
        CancellationToken ct = default);

    /// <summary>Obtiene el historial de lecturas de un sensor.</summary>
    Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>> 
        GetSensorReadingsAsync(string sensorId, CancellationToken ct = default);

    /// <summary>Obtiene lecturas de sensor dentro de un rango de fechas.</summary>
    Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>> 
        GetSensorReadingsByDateRangeAsync(string sensorId, DateTime startDate, DateTime endDate, 
        CancellationToken ct = default);

    // ── Acciones de Dispositivos (Rúbrica: "Debe guardar acciones tomadas: LED on/off") ──

    /// <summary>Registra una acción de dispositivo (LED ON/OFF, GATE, etc.) con timestamp (rúbrica).</summary>
    Task<bool> LogDeviceActionAsync(string deviceId, string action, DateTime timestamp, 
        CancellationToken ct = default);

    /// <summary>Obtiene el historial de acciones de un dispositivo.</summary>
    Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>> 
        GetDeviceActionsAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Obtiene acciones de dispositivo dentro de un rango de fechas.</summary>
    Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>> 
        GetDeviceActionsByDateRangeAsync(string deviceId, DateTime startDate, DateTime endDate, 
        CancellationToken ct = default);

    // ── Operaciones sobre alertas (Full Capacity Alert: ID, Type, Message, Timestamp) ──

    /// <summary>Registra una alerta con su tipo, mensaje y timestamp (rúbrica).</summary>
    Task<bool> LogAlertAsync(string alertId, string type, string message, DateTime timestamp, 
        CancellationToken ct = default);

    /// <summary>Obtiene el historial de alertas.</summary>
    Task<IEnumerable<(string Id, string Type, string Message, DateTime Timestamp)>> 
        GetAlertsAsync(CancellationToken ct = default);

    /// <summary>Obtiene alertas de un tipo específico.</summary>
    Task<IEnumerable<(string Id, string Type, string Message, DateTime Timestamp)>> 
        GetAlertsByTypeAsync(string type, CancellationToken ct = default);

    /// <summary>Obtiene alertas dentro de un rango de fechas.</summary>
    Task<IEnumerable<(string Id, string Type, string Message, DateTime Timestamp)>> 
        GetAlertsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>Elimina alertas antiguas (limpieza).</summary>
    Task<bool> DeleteAlertsByDateAsync(DateTime beforeDate, CancellationToken ct = default);
}
