using SmartParkingLot.Controllers;
using System;

namespace SmartParkingLot.Domain;

public class EntryRequest : Request
{
    public bool Approved { get; private set; }
    public EntryRequest(string vehiclePlate){ VehiclePlate = vehiclePlate; }

    // GRASP - Polymorphism: Cada tipo de Request implementa su propia lógica de ejecución
    public override void Execute(GateController gc)
    {
        Console.WriteLine($"\n[EntryRequest] Solicitud recibida: Vehículo '{VehiclePlate}' a las {Timestamp:HH:mm:ss}");

        Approved = gc.CapacityService.HasAvailableSpots();

        if (Approved)
        {
            var spot = gc.CapacityService.ReserveSpot();
            if (spot != null)
            {
                Console.WriteLine($"[EntryRequest] Espacio asignado: {spot}");
                gc.OpenGate(GateId);
            }
            else
            {
                Approved = false;
                var reading = new GateSensorReading(VehiclePlate, GateId);
                gc.AlertService.GenerateAlert(reading);
                Console.WriteLine($"[EntryRequest] Error al reservar espacio para {VehiclePlate}. Puerta permanece CERRADA.");
            }
        }
        else
        {
            var reading = new GateSensorReading(VehiclePlate, GateId);
            gc.AlertService.GenerateAlert(reading);
            Console.WriteLine($"[EntryRequest] Sin espacios disponibles para {VehiclePlate}. Puerta permanece CERRADA.");
        }
    }
}