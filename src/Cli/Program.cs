using static SmartParkingLot.Core.Constants;

using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;
using SmartParkingLot.Application;
using SmartParkingLot.Hardware;
using SmartParkingLot.Persistence;

// ═══════════════════════════════════════════════════════════════════
// GRASP - Composition Root (Creator): Program.cs es el único lugar
// donde se ensamblan las dependencias de toda la aplicación.
// Aquí se aplica Dependency Injection manual con top-level statements.
// ═══════════════════════════════════════════════════════════════════

// ── 0. Inicializar la capa de persistencia (SQLite) ──
// SOLID - DIP: El Composition Root instancia la BD; los servicios solo conocen IParkingRepository
var dataDir = "./data";
if (!Directory.Exists(dataDir))
    Directory.CreateDirectory(dataDir);

var connectionString = $"Data Source={Path.Combine(dataDir, "smartparkingdb.db")};Version=3;";
var dbInitializer = new DatabaseInitializer(connectionString);
await dbInitializer.InitializeAsync();

Console.WriteLine("[Persistence] ✓ Base de datos SQLite inicializada correctamente.");

// Inyectar el repositorio (IParkingRepository)
IParkingRepository parkingRepository = new SqliteParkingRepository(connectionString);

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

// ── 2.1 Bridge serial: conecta Arduino fisico con sensores via ISensorCapture (DIP) ──
// Mapeo: hardware ID -> (spot ID del dominio, sensor)
var sensorMap = new Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)>
{
    ["IR1"] = ("A1", spotSensorA1)
};

using var bridge = new ArduinoSerialBridge("COM6", 9600, sensorMap);
bridge.StartListening();

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

// ── Fase 5: Persistencia — Registrar requests en auditoría ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 5: Persistencia de datos (SQLite)");
Console.WriteLine("══════════════════════════════════════════════════");

// GRASP - Information Expert: GateController sabe qué requests fueron aprobados
// SOLID - DIP: El controller no conoce los detalles de persistencia
// Registrar algunos requests en la BD para auditoría
foreach (var request in entryRequests)
{
    await parkingRepository.LogRequestAsync(
        requestId: $"REQ-{Guid.NewGuid().ToString().Substring(0, 8)}",
        vehiclePlate: request.VehiclePlate,
        requestType: "ENTRY",
        lotId: "LOT-01",
        timestamp: request.Timestamp,
        approved: request.Approved);
}

Console.WriteLine("[Persistence] ✓ Requests registrados en la base de datos.");

// ── Fase 6: Cargar datos desde la BD —Demostración de lectura ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 6: Lectura desde BD — Validación");
Console.WriteLine("══════════════════════════════════════════════════");

// Cargar el lote desde BD
var loadedLot = await parkingRepository.GetParkingLotByIdAsync("LOT-01");
if (loadedLot is not null)
{
    Console.WriteLine($"[BD] Lote cargado: {loadedLot.Name}");
    Console.WriteLine($"[BD] Espacios totales: {loadedLot.TotalSpots}");
    Console.WriteLine($"[BD] Espacios disponibles: {loadedLot.AvailableSpots}");

    foreach (var spot in loadedLot.GetSpots())
        Console.WriteLine($"  {spot}");
}

// Cargar espacios disponibles
var availableSpots = await parkingRepository.GetAvailableSpotsAsync("LOT-01");
Console.WriteLine($"\n[BD] Espacios disponibles (consulta directa): {availableSpots.Count()}");

// Cargar historial de requests
var history = await parkingRepository.GetRequestHistoryAsync("VH-001");
Console.WriteLine($"\n[BD] Historial de VH-001:");
foreach (var (id, plate, type, timestamp, approved) in history)
{
    Console.WriteLine($"  - {type} @ {timestamp:HH:mm:ss} | Aprobado: {approved}");
}

// ── Fase 7: Lecturas de Sensores y Acciones de Dispositivos (Rúbrica) ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 7: Almacenamiento de Sensores y Dispositivos (Rúbrica)");
Console.WriteLine("══════════════════════════════════════════════════");

// RÚBRICA: Guardar lecturas de sensores (valor + timestamp)
await parkingRepository.LogSensorReadingAsync("SEN-GATE-01", "plate:VH-001", DateTime.Now.AddSeconds(-10));
await parkingRepository.LogSensorReadingAsync("SEN-SPOT-A1", "occupied:true", DateTime.Now.AddSeconds(-8));
await parkingRepository.LogSensorReadingAsync("SEN-SPOT-A1", "distance:15cm", DateTime.Now.AddSeconds(-5));

Console.WriteLine("[Sensores] ✓ 3 lecturas de sensores guardadas con timestamp");

// Consultar historial de lecturas
var sensorReadings = await parkingRepository.GetSensorReadingsAsync("SEN-SPOT-A1");
Console.WriteLine($"\n[Sensores] Historial de SEN-SPOT-A1:");
foreach (var (id, sensorId, value, timestamp) in sensorReadings)
{
    Console.WriteLine($"  - Lectura: {value} @ {timestamp:HH:mm:ss}");
}

// RÚBRICA: Guardar acciones de dispositivos (LED ON/OFF, GATE, etc. + timestamp)
await parkingRepository.LogDeviceActionAsync("LED_1", "ON", DateTime.Now.AddSeconds(-9));
await parkingRepository.LogDeviceActionAsync("GATE_G-01", "OPEN_90deg", DateTime.Now.AddSeconds(-7));
await parkingRepository.LogDeviceActionAsync("LED_1", "OFF", DateTime.Now.AddSeconds(-3));

Console.WriteLine("\n[Dispositivos] ✓ 3 acciones de dispositivos guardadas con timestamp");

// Consultar historial de acciones
var deviceActions = await parkingRepository.GetDeviceActionsAsync("LED_1");
Console.WriteLine($"\n[Dispositivos] Historial de acciones - LED_1:");
foreach (var (id, deviceId, action, timestamp) in deviceActions)
{
    Console.WriteLine($"  - Acción: {action} @ {timestamp:HH:mm:ss}");
}

// ── Resumen de alertas ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("  FASE 8: Resumen de alertas");
Console.WriteLine("══════════════════════════════════════════════════");
alertService.NotifyAll();

// ── Conclusión ──
Console.WriteLine("\n══════════════════════════════════════════════════");
Console.WriteLine("✓ Simulación completada exitosamente");
Console.WriteLine("  → Composición Root: DI manual implementado");
Console.WriteLine("  → BD: SQLite con seeding programático");
Console.WriteLine("  → Persistencia: IParkingRepository con Dapper");
Console.WriteLine("  → Auditoría: Requests registrados en BD");
Console.WriteLine("  → Sensores: Lecturas (valor + timestamp) persistidas");
Console.WriteLine("  → Dispositivos: Acciones (LED ON/OFF, GATE, etc.) persistidas");
Console.WriteLine("══════════════════════════════════════════════════\n");
