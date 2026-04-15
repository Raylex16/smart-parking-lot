using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Core;

public class ExitRequest : Request
{
    public ExitRequest(string vehiclePlate)
    {
        VehiclePlate = vehiclePlate;
    }

    // GRASP - Polymorphism: ExitRequest solo abre la puerta de salida.
    // La liberación del espacio es responsabilidad del sensor del spot (Information Expert),
    // que al detectar el cambio notifica a CapacityService.UpdateSpotState().
    public override void Execute(IGateRequestHandler handler)
    {
        Console.WriteLine($"\n[ExitRequest] Solicitud de salida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");
        handler.OpenGate(GateId);
    }
}
