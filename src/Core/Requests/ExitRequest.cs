using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Core;

public class ExitRequest : Request
{
    public ExitRequest(string vehiclePlate)
    {
        VehiclePlate = vehiclePlate;
    }

    public override void Execute(IGateRequestHandler handler)
    {
        Console.WriteLine($"\n[ExitRequest] Solicitud de salida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");
        handler.OpenGate(GateId);
    }
}
