using SmartParkingLot.Controllers;
using SmartParkingLot.Domain;
using SmartParkingLot.Services;

var lot = new ParkingLot("LOT-01", "Campus Norte");
lot.AddSpot(new ParkingSpot("A1", "Estándar",  "Planta Baja"));
lot.AddSpot(new ParkingSpot("A2", "Estándar",  "Planta Baja"));
lot.AddSpot(new ParkingSpot("B1", "Compacto",  "Nivel 1"));

ICapacityService capacityService = new CapacityService(lot);
IGateController  gateController  = new GateController(capacityService);

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║   Smart Parking Lot — Sistema de Entrada  ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"Parqueadero : {lot.Name}");
Console.WriteLine($"Espacios    : {lot.TotalSpots} totales | {lot.AvailableSpots} disponibles");

var requests = new EntryRequest[]
{
    new("VH-001", "Sedán"),
    new("VH-002", "SUV"),
    new("VH-003", "Camión"),
    new("VH-004", "Sedán"),
};

foreach (var request in requests)
{
    Console.WriteLine("\n──────────────────────────────────────────");
    bool granted = gateController.ProcessEntryRequest(request);
    Console.WriteLine($"[Resultado] Acceso: {(granted ? "CONCEDIDO ✓" : "DENEGADO ✗")} | Espacios restantes: {lot.AvailableSpots}");
}

Console.WriteLine("\n══════════════════════════════════════════");
Console.WriteLine("Estado final de espacios:");
foreach (var spot in lot.GetSpots())
    Console.WriteLine($"  {spot}");
