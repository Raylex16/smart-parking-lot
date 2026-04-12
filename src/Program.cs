using SmartParkingLot.Controllers;
using SmartParkingLot.Domain;
using SmartParkingLot.Services;

// =============================================================================
// Smart Parking Lot — Composition Root
//
// Program.cs actúa únicamente como raíz de composición (Dependency Injection
// manual). Su única responsabilidad es instanciar dependencias y conectarlas.
// Toda la lógica de negocio vive en Domain/, Services/ y Controllers/.
//
// GRASP - Low Coupling: Program.cs conoce todas las clases concretas porque
// ES el único lugar donde es   o es aceptable (composition root). El resto del
// sistema solo conoce interfaces o clases de dominio.
// =============================================================================

// --- Configuración del dominio ---
var lot = new ParkingLot("LOT-01", "Campus Norte");
lot.AddSpot(new ParkingSpot("A1", "Estándar",  "Planta Baja"));
lot.AddSpot(new ParkingSpot("A2", "Estándar",  "Planta Baja"));
lot.AddSpot(new ParkingSpot("B1", "Compacto",  "Nivel 1"));

// --- Inyección de dependencias manual ---
ICapacityService capacityService = new CapacityService(lot);
IGateController  gateController  = new GateController(capacityService);

// --- Simulación del caso de uso ---
Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║   Smart Parking Lot — Sistema de Entrada  ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"Parqueadero : {lot.Name}");
Console.WriteLine($"Espacios    : {lot.TotalSpots} totales | {lot.AvailableSpots} disponibles");

var requests = new EntryRequest[]
{
    new("VH-001", "Sedán"),   // Debe entrar  → espacio A1
    new("VH-002", "SUV"),     // Debe entrar  → espacio A2
    new("VH-003", "Camión"),  // Debe entrar  → espacio B1
    new("VH-004", "Sedán"),   // Debe ser RECHAZADO (sin espacios)
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
