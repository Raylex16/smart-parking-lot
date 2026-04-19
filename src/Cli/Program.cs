using SmartParkingLot.Application;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Cli;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;
using SmartParkingLot.Persistence;


// Program.cs — Composition Root (manual DI con top-level statements).
// GRASP - Creator: ensambla todas las dependencias del sistema en un único lugar.


// ── 1. Inicializar persistencia ──

var dbPath = Path.Combine(AppContext.BaseDirectory, DB_FOLDER_NAME, DB_FILE_NAME);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var connectionString = $"Data Source={dbPath};Version=3;";

var initializer = new DatabaseInitializer(connectionString);
await initializer.InitializeAsync();

IParkingRepository repository = new SqliteParkingRepository(connectionString);


// ── 2. Cargar el parqueadero desde BD (estado persistente) ──

var lot = await repository.GetParkingLotByIdAsync(DEFAULT_LOT_ID);
if (lot is null)
{
    Console.WriteLine($"[ERROR] No se encontró el parqueadero '{DEFAULT_LOT_ID}' en la BD.");
    return;
}


// ── 3. Bus de eventos en proceso ──

IEventPublisher bus = new InProcessEventBus();


// ── 4. Bridge serial con Arduino (opcional — continúa si no hay hardware) ──

using var bridge = new ArduinoSerialBridge(DEFAULT_PORT_NAME, DEFAULT_BAUD_RATE, bus);
using var dispatcher = new SerialCommandDispatcher(bridge);


// ── 5. Crear sensores (uno por cada spot + sensor de puerta) para el menú ──

var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "LPR");

var spotSensors = new Dictionary<string, Sensor<SpotSensorReading>>();
foreach (var s in lot.GetSpots())
{
    spotSensors[s.Id] = new Sensor<SpotSensorReading>($"SEN-SPOT-{s.Id}", "Ultrasonido");
}


// ── 6. Caso de uso: lecturas del sensor -> dominio ──
// El mapeo incluye tanto los IDs del hardware Arduino (IR1) como los IDs
// de los sensores del menú (SEN-SPOT-A1). Así, tanto las lecturas del Arduino
// como las del menú disparan el mismo use case (HandleSensorReadingUseCase).

var sensorToSpot = new Dictionary<string, string> { ["IR1"] = "A1" };
foreach (var s in spotSensors.Values)
    sensorToSpot[s.Id] = s.Id.Replace("SEN-SPOT-", "");

var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
bus.Subscribe<SensorReadingReceived>(handleReading.Handle);


// ── 7. Handler: eventos de dominio -> comandos al actuador ──

var spotToActuator = new Dictionary<string, string> { ["A1"] = "LED1" };
var occupancyHandler = new SpotOccupancyChangedHandler(dispatcher, spotToActuator);

foreach (var spot in lot.GetSpots())
    spot.OccupancyChanged += occupancyHandler.Handle;


// ── 7.1. Persistencia de cambios de ocupación (cualquier fuente: Arduino o menú) ──
// Cuando un spot cambia de estado, persistimos el cambio en BD automáticamente.

foreach (var spot in lot.GetSpots())
{
    spot.OccupancyChanged += evt =>
    {
        _ = repository.UpdateSpotStatusAsync(evt.SpotId, evt.IsOccupied);
    };
}


// ── 8. Instanciar servicios de aplicación ──

ICapacityService capacityService = new CapacityService(lot);
IAlertService alertService = new AlertService();
var gateController = new GateController(capacityService, alertService);


// ── 9. Registrar puertas físicas ──

gateController.RegisterGate(ENTRY_GATE_ID, new Gate(ENTRY_GATE_ID, GateType.ENTRY, ENTRY_GATE_PIN));
gateController.RegisterGate(EXIT_GATE_ID, new Gate(EXIT_GATE_ID, GateType.EXIT, EXIT_GATE_PIN));


// ── 10. Arrancar bridge ──

bridge.StartListening();


// ── 11. Ejecutar menú interactivo ──

var menu = new ConsoleMenu(lot, gateController, capacityService, repository, bus, spotSensors, gateSensor);
await menu.RunAsync();
