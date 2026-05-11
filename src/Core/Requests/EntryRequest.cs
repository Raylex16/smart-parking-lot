using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Core;

public class EntryRequest : Request
{
    private const string LogSource = "EntryRequest";

    public bool Approved { get; private set; }
    public EntryRequest(string vehiclePlate) { VehiclePlate = vehiclePlate; }

    public override async Task ExecuteAsync(IGateRequestHandler handler)
    {
        handler.Logger.Log(LogLevel.Info, LogSource,
            $"Solicitud recibida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");

        if (!handler.CapacityService.HasAvailableSpots())
        {
            Approved = false;
            handler.AlertService.GenerateAlert(new GateSensorReading(VehiclePlate, GateId));
            handler.Logger.Log(LogLevel.Warning, LogSource,
                $"Sin espacios disponibles para {VehiclePlate}. Puerta permanece CERRADA.");
            return;
        }

        if (!await handler.AccessPolicy.CanEnterAsync(this).ConfigureAwait(false))
        {
            Approved = false;
            handler.AlertService.GenerateAlert(new GateSensorReading(VehiclePlate, GateId));
            handler.Logger.Log(LogLevel.Warning, LogSource,
                $"Política de acceso rechazó el ingreso de {VehiclePlate}. Puerta permanece CERRADA.");
            return;
        }

        var spot = handler.CapacityService.ReserveSpot();
        if (spot is null)
        {
            Approved = false;
            handler.AlertService.GenerateAlert(new GateSensorReading(VehiclePlate, GateId));
            handler.Logger.Log(LogLevel.Error, LogSource,
                $"Error al reservar espacio para {VehiclePlate}. Puerta permanece CERRADA.");
            return;
        }

        Approved = true;
        handler.Logger.Log(LogLevel.Info, LogSource, $"Espacio asignado: {spot}");
        handler.OpenGate(GateId);
    }
}
