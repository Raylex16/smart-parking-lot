using SmartParkingLot.Application;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;
using SmartParkingLot.Persistence;

namespace SmartParkingLot.Cli;

// GRASP - Creator: ParkingLotApp es responsable de ensamblar todas las
// dependencias del sistema porque es quien las utiliza y las instancia
// (principio de Creator: quien crea, contiene o registra un objeto
// debe ser responsable de construirlo).
public sealed class ParkingLotApp
{
    public async Task RunAsync()
    {
        // ── 1. Cargar configuración de hardware ──
        var configPath = Path.Combine(AppContext.BaseDirectory, "hardware.json");
        var hwConfig = HardwareConfig.Load(configPath);


        // ── 2. Inicializar persistencia ──
        var dbPath = Path.Combine(AppContext.BaseDirectory, DB_FOLDER_NAME, DB_FILE_NAME);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var connectionString = $"Data Source={dbPath};";

        var initializer = new DatabaseInitializer(connectionString);
        await initializer.InitializeAsync();

        IParkingRepository repository = new SqliteParkingRepository(connectionString);

        // Sincroniza los spots definidos en hardware.json con la BD (idempotente).
        // Agregar un sensor al JSON registra el spot automáticamente en el siguiente arranque.
        foreach (var mapping in hwConfig.Sensors)
            await repository.EnsureSpotExistsAsync(
                mapping.SpotId, DEFAULT_LOT_ID,
                mapping.Address, mapping.Type, mapping.Floor);


        // ── 3. Cargar el parqueadero desde BD (estado persistente) ──
        var lot = await repository.GetParkingLotByIdAsync(DEFAULT_LOT_ID);
        if (lot is null)
        {
            Console.WriteLine($"[ERROR] No se encontró el parqueadero '{DEFAULT_LOT_ID}' en la BD.");
            return;
        }


        // ── 4. Bus de eventos en proceso ──
        IEventPublisher bus = new InProcessEventBus();


        // ── 5. Bridge serial con Arduino (puerto y baudRate vienen de hardware.json) ──
        using var bridge = new ArduinoSerialBridge(hwConfig.Port, hwConfig.BaudRate, bus);
        using var dispatcher = new SerialCommandDispatcher(bridge);


        // ── 5. Crear sensores (uno por cada spot + sensor de puerta) para el menú ──
        var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "LPR");

        var spotSensors = new Dictionary<string, Sensor<SpotSensorReading>>();
        foreach (var s in lot.GetSpots())
        {
            spotSensors[s.Id] = new Sensor<SpotSensorReading>($"SEN-SPOT-{s.Id}", "Ultrasonido");
        }


        // ── 6. Caso de uso: lecturas del sensor -> dominio ──
        // sensorToSpot combina los IDs de hardware (IR1…) definidos en hardware.json
        // y los IDs de sensores de menú (SEN-SPOT-A1…) para que ambas fuentes disparen
        // el mismo use case.
        var sensorToSpot = new Dictionary<string, string>(hwConfig.BuildSensorToSpot());
        foreach (var s in spotSensors.Values)
            sensorToSpot[s.Id] = s.Id.Replace("SEN-SPOT-", "");

        var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
        bus.Subscribe<SensorReadingReceived>(handleReading.Handle);


        // ── 7. Handler: eventos de dominio -> comandos al actuador ──
        var spotToActuator = hwConfig.BuildSpotToActuator();
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


        // ── 10. Arrancar bridge en silencio para leer Arduino y persistir datos.
        // Los mensajes de consola solo se habilitan cuando el usuario selecciona opción 8.
        bridge.ConsoleLoggingEnabled = false;
        bridge.StartListening();

        // ── 11. Ejecutar menú interactivo ──

        var menu = new ConsoleMenu(lot, gateController, capacityService, repository, bus, spotSensors, gateSensor, bridge, dispatcher);
        await menu.RunAsync();
    }
}
