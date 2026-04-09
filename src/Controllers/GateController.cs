using SmartParkingLot.Domain;
using SmartParkingLot.Services;

namespace SmartParkingLot.Controllers;

/// <summary>
/// Controlador del evento de sistema: solicitud de entrada al parqueadero.
/// Coordina el flujo principal del caso de uso sin contener lógica de negocio.
/// </summary>
/// <remarks>
/// GRASP - Controller:
/// GateController es el receptor del evento del sistema "un vehículo pide entrar".
/// Actúa como fachada entre el mundo exterior (sensores, UI, API) y los servicios
/// de dominio. No hace cálculos de negocio; delega en ICapacityService.
///
/// GRASP - Low Coupling:
/// GateController depende únicamente de la interfaz ICapacityService, no de
/// implementaciones concretas ni de hardware físico. El comportamiento de la
/// puerta física se aísla en el método privado OpenGate(), evitando que los
/// detalles de hardware contaminen la lógica de coordinación.
/// </remarks>
public class GateController : IGateController
{
    // GRASP - Low Coupling: dependencia sobre interfaz, no sobre clase concreta.
    private readonly ICapacityService _capacityService;

    public GateController(ICapacityService capacityService)
    {
        _capacityService = capacityService;
    }

    /// <summary>
    /// Punto de entrada del evento de sistema.
    /// Orquesta la verificación de capacidad, la reserva de espacio y la apertura
    /// de puerta. Retorna true si el acceso fue concedido.
    /// </summary>
    /// <remarks>
    /// GRASP - Controller: recibe el evento de sistema (EntryRequest) y coordina
    /// la respuesta sin implementar él mismo la lógica de disponibilidad.
    /// </remarks>
    public bool ProcessEntryRequest(EntryRequest request)
    {
        Console.WriteLine($"\n[GateController] Solicitud recibida: Vehículo '{request.VehicleId}' ({request.VehicleType}) a las {request.Timestamp:HH:mm:ss}");

        // GRASP - Low Coupling: GateController pregunta al servicio, no a ParkingLot directamente.
        if (!_capacityService.HasAvailableSpots())
        {
            Console.WriteLine("[GateController] Sin espacios disponibles. Puerta permanece CERRADA.");
            return false;
        }

        // GRASP - Information Expert: CapacityService (que delega en ParkingLot) decide
        // qué espacio asignar; GateController solo reacciona al resultado.
        var assignedSpot = _capacityService.ReserveSpot();

        if (assignedSpot is null)
        {
            Console.WriteLine("[GateController] Error al reservar espacio. Puerta permanece CERRADA.");
            return false;
        }

        Console.WriteLine($"[GateController] Espacio asignado: {assignedSpot}");
        OpenGate();
        return true;
    }

    /// <summary>
    /// Encapsula la interacción con el hardware físico de la puerta.
    /// </summary>
    /// <remarks>
    /// GRASP - Low Coupling:
    /// Los detalles del hardware (señal eléctrica, protocolo IoT, etc.) están
    /// confinados aquí. Si el hardware cambia, solo este método se modifica;
    /// el flujo de negocio permanece intacto.
    /// </remarks>
    private static void OpenGate()
    {
        Console.WriteLine("[Gate] >>> PUERTA ABIERTA <<<");
        // Producción: enviar señal al controlador físico (GPIO, MQTT, HTTP, etc.)
    }
}
