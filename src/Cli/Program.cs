using static SmartParkingLot.Core.Constants;

using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;
using SmartParkingLot.Application;
using SmartParkingLot.Hardware;

// ═══════════════════════════════════════════════════════════════════
// GRASP - Composition Root (Creator): Program.cs es el único lugar
// donde se ensamblan las dependencias de toda la aplicación.
// Aquí se aplica Dependency Injection manual con top-level statements.
// ═══════════════════════════════════════════════════════════════════

// ── 1. Crear el parqueadero y sus espacios ──
var lot = new ParkingLot("LOT-01", "Campus Barcelona", ParkingMode.AUTOMATIC);
lot.AddSpot(new ParkingSpot("A1", "Zona-A Fila-1", "Estándar",  "Planta Baja"));
lot.AddSpot(new ParkingSpot("A2", "Zona-A Fila-2", "Estándar",  "Planta Baja"));
lot.AddSpot(new ParkingSpot("B1", "Zona-B Fila-1", "Compacto",  "Nivel 1"));

// ── 2. Crear sensores simulados (IoT) ──
var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "Cámara LPR");
var spotSensorA1 = new Sensor<SpotSensorReading>("SEN-SPOT-A1", "Ultrasonido");
var spotSensorA2 = new Sensor<SpotSensorReading>("SEN-SPOT-A2", "Ultrasonido");
var spotSensorB1 = new Sensor<SpotSensorReading>("SEN-SPOT-B1", "Ultrasonido");

// ── 3. Crear puertas ──
var entryGate = new Gate("G-01", GateType.ENTRY, DefaultPin);
var exitGate  = new Gate("G-02", GateType.EXIT, DefaultPin);

// ── 4. Inyectar servicios (GRASP - Creator + Low Coupling) ──
// SOLID - DIP: Las variables se tipan con interfaces (ICapacityService, IAlertService),
// demostrando que los módulos de alto nivel dependen de abstracciones.
ICapacityService capacityService = new CapacityService(lot);
IAlertService alertService = new AlertService();
var gateController = new GateController(capacityService, alertService);

gateController.RegisterGate("G-01", entryGate);
gateController.RegisterGate("G-02", exitGate);

// ── 5. Crear usuario administrador ──
var admin = new User(lot);

// ═══════════════════════════════════════════════════════════════════
// SIMULACIÓN
// ═══════════════════════════════════════════════════════════════════

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║   Smart Parking Lot — Simulación IoT Completa   ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine($"Parqueadero : {lot.Name}");
Console.WriteLine($"Modo        : {lot.Mode}");
Console.WriteLine($"Espacios    : {lot.TotalSpots} totales | {lot.AvailableSpots} disponibles");
Console.WriteLine($"Sensores    : {gateSensor}, {spotSensorA1}, {spotSensorA2}, {spotSensorB1}");

// ── Fase 1: Solicitudes de entrada ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 1: Solicitudes de entrada");
Console.WriteLine("══════════════════════════════════════════════════");

var entryRequests = new EntryRequest[]
{
    new("VH-001"),
    new("VH-002"),
    new("VH-003"),
    new("VH-004"),  // Este debería ser denegado (sin espacio)
};

foreach (var request in entryRequests)
{
    Console.WriteLine("\n──────────────────────────────────────────");

    // Simular lectura del sensor de puerta (cámara LPR detecta placa)
    var gateReading = new GateSensorReading(request.VehiclePlate, request.GateId);
    gateSensor.CaptureReading(gateReading);

    gateController.HandleRequest(request);
    Console.WriteLine($"[Resultado] Acceso: {(request.Approved ? "CONCEDIDO ✓" : "DENEGADO ✗")} | Espacios restantes: {lot.AvailableSpots}");
}

// ── Fase 2: Simulación de sensores de spot ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 2: Lecturas de sensores de espacio");
Console.WriteLine("══════════════════════════════════════════════════");

// Los sensores de spot confirman la ocupación
spotSensorA1.CaptureReading(new SpotSensorReading("A1", true));
spotSensorA2.CaptureReading(new SpotSensorReading("A2", true));
spotSensorB1.CaptureReading(new SpotSensorReading("B1", true));

// ── Fase 3: Consulta administrativa ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 3: Consulta administrativa");
Console.WriteLine("══════════════════════════════════════════════════");
admin.CheckAvailability();
admin.ConfigSystem();

// ── Fase 4: Salida de un vehículo ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 4: Salida de vehículo");
Console.WriteLine("══════════════════════════════════════════════════");

// La puerta de salida solo abre; no sabe qué spot liberar
var exitRequest = new ExitRequest("VH-001") { GateId = "G-02" };
gateController.HandleRequest(exitRequest);

// El sensor del spot detecta que el vehículo se fue y notifica al servicio de capacidad
var releaseReading = new SpotSensorReading("A1", false);
spotSensorA1.CaptureReading(releaseReading);
capacityService.UpdateSpotState(releaseReading);

Console.WriteLine($"\n[Resultado] Espacios disponibles tras salida: {lot.AvailableSpots}");

// ── Fase 5: Resumen de alertas ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 5: Resumen de alertas");
Console.WriteLine("══════════════════════════════════════════════════");
alertService.NotifyAll();

// ── Estado final ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("Estado final de espacios:");
foreach (var spot in lot.GetSpots())
    Console.WriteLine($"  {spot}");
