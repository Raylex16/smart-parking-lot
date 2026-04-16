using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Core;

public class EntryRequest : Request
{
    public bool Approved { get; private set; }
    public EntryRequest(string vehiclePlate){ VehiclePlate = vehiclePlate; }

    // GRASP - Polymorphism: Cada tipo de Request implementa su propia lógica de ejecución
    public override void Execute(IGateRequestHandler handler)
    {
        Console.WriteLine($"\n[EntryRequest] Solicitud recibida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");

        Approved = handler.CapacityService.HasAvailableSpots();

        if (Approved)
        {
            var spot = handler.CapacityService.ReserveSpot();
            if (spot != null)
            {
                Console.WriteLine($"[EntryRequest] Espacio asignado: {spot}");
                handler.OpenGate(GateId);
            }
            else
            {
                Approved = false;
                var reading = new GateSensorReading(VehiclePlate, GateId);
                handler.AlertService.GenerateAlert(reading);
                Console.WriteLine($"[EntryRequest] Error al reservar espacio para {VehiclePlate}. Puerta permanece CERRADA.");
            }
        }
        else
        {
            var reading = new GateSensorReading(VehiclePlate, GateId);
            handler.AlertService.GenerateAlert(reading);
            Console.WriteLine($"[EntryRequest] Sin espacios disponibles para {VehiclePlate}. Puerta permanece CERRADA.");
        }
    }
}
