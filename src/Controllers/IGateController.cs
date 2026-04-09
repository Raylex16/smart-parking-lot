using SmartParkingLot.Domain;

namespace SmartParkingLot.Controllers;

/// <summary>
/// Contrato del controlador de la puerta de entrada.
/// </summary>
/// <remarks>
/// GRASP - Controller:
/// Define el punto de entrada para el evento de sistema "solicitud de acceso".
/// Abstraer esto permite reemplazar el controlador (p. ej. por uno basado en
/// WebSocket o MQTT para IoT) sin afectar el resto del sistema.
/// </remarks>
public interface IGateController
{
    /// <summary>
    /// Procesa una solicitud de entrada y retorna true si la puerta fue abierta.
    /// </summary>
    bool ProcessEntryRequest(EntryRequest request);
}
