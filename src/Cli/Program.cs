using SmartParkingLot.Application;
using SmartParkingLot.Cli;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;
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


// ── 3. Instanciar servicios de aplicación ──

ICapacityService capacityService = new CapacityService(lot);
IAlertService alertService = new AlertService();
var gateController = new GateController(capacityService, alertService);


// ── 4. Registrar puertas físicas ──

gateController.RegisterGate(ENTRY_GATE_ID, new Gate(ENTRY_GATE_ID, GateType.ENTRY, ENTRY_GATE_PIN));
gateController.RegisterGate(EXIT_GATE_ID, new Gate(EXIT_GATE_ID, GateType.EXIT, EXIT_GATE_PIN));


// ── 5. Crear sensores (uno por cada spot cargado + un sensor de puerta) ──

var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "LPR");

var spotSensors = new Dictionary<string, Sensor<SpotSensorReading>>();
foreach (var spot in lot.GetSpots())
{
    spotSensors[spot.Id] = new Sensor<SpotSensorReading>($"SEN-SPOT-{spot.Id}", "Ultrasonido");
}


// ── 6. Bridge serial con Arduino (opcional — continúa si no hay hardware) ──

var sensorMap = new Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)>();
if (spotSensors.TryGetValue("A1", out var sensorA1))
{
    sensorMap["IR1"] = ("A1", sensorA1);
}

using var bridge = new ArduinoSerialBridge(DEFAULT_PORT_NAME, DEFAULT_BAUD_RATE, sensorMap);
bridge.StartListening(); // Si el puerto no existe, imprime advertencia y continúa.


// ── 7. Ejecutar menú interactivo ──

var menu = new ConsoleMenu(lot, gateController, capacityService, repository, spotSensors, gateSensor);
await menu.RunAsync();
