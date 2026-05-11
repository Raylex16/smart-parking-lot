using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Core;

public class ExitRequest : Request
{
    public ExitRequest(string vehiclePlate)
    {
        VehiclePlate = vehiclePlate;
    }

    public override Task ExecuteAsync(IGateRequestHandler handler)
    {
        handler.Logger.Log(LogLevel.Info, "ExitRequest",
            $"Solicitud de salida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");
        handler.OpenGate(GateId);
        return Task.CompletedTask;
    }
}
