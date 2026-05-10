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
        handler.Logger.Log(LogLevel.Info, "ExitRequest",
            $"Solicitud de salida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");
        handler.OpenGate(GateId);
    }
}
