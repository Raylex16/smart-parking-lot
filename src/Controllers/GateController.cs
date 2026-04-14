using SmartParkingLot.Domain;
using SmartParkingLot.Services;

namespace SmartParkingLot.Controllers;

public class GateController : IGateController
{
    private readonly ICapacityService _capacityService;

    public GateController(ICapacityService capacityService)
    {
        _capacityService = capacityService;
    }

    public bool ProcessEntryRequest(EntryRequest request)
    {
        Console.WriteLine($"\n[GateController] Solicitud recibida: Vehículo '{request.VehicleId}' ({request.VehicleType}) a las {request.Timestamp:HH:mm:ss}");

        if (!_capacityService.HasAvailableSpots())
        {
            Console.WriteLine("[GateController] Sin espacios disponibles. Puerta permanece CERRADA.");
            return false;
        }

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

    private static void OpenGate()
    {
        Console.WriteLine("[Gate] >>> PUERTA ABIERTA <<<");
    }
}
