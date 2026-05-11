using SmartParkingLot.Application;
using SmartParkingLot.Application.Display;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Application.Recognition;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;
using SmartParkingLot.Persistence;

namespace SmartParkingLot.Cli;

public sealed class ParkingLotApp
{
    public async Task RunAsync()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "hardware.json");
        var hwConfig = HardwareConfig.Load(configPath);

        var dbPath = Path.Combine(AppContext.BaseDirectory, DB_FOLDER_NAME, DB_FILE_NAME);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        var consoleLogger = new ConsoleLogger(LogLevel.Info);
        var fileLogger = new FileLogger(logsDir, LogLevel.Debug);
        ILogger logger = new CompositeLogger(consoleLogger, fileLogger);

        var connectionString = $"Data Source={dbPath};";

        var initializer = new DatabaseInitializer(connectionString, logger);
        await initializer.InitializeAsync();

        IParkingRepository repository = new SqliteParkingRepository(connectionString);

        foreach (var mapping in hwConfig.Sensors)
            await repository.EnsureSpotExistsAsync(
                mapping.SpotId, DEFAULT_LOT_ID,
                mapping.Address, mapping.Type, mapping.Floor);

        var validSpotIds = hwConfig.Sensors.Select(m => m.SpotId).ToList();
        var removed = await repository.RemoveOrphanSpotsAsync(DEFAULT_LOT_ID, validSpotIds);
        if (removed > 0)
            logger.Info("ParkingLotApp", $"Eliminados {removed} spot(s) huérfanos no presentes en hardware.json");

        var lot = await repository.GetParkingLotByIdAsync(DEFAULT_LOT_ID);
        if (lot is null)
        {
            logger.Error("ParkingLotApp", $"No se encontró el parqueadero '{DEFAULT_LOT_ID}' en la BD.");
            return;
        }

        IEventPublisher bus = new InProcessEventBus();

        using var bridge = new ArduinoSerialBridge(hwConfig.Port, hwConfig.BaudRate, bus, logger);
        using var dispatcher = new SerialCommandDispatcher(bridge, logger);

        var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "LPR", logger);

        var spotSensors = new Dictionary<string, Sensor<SpotSensorReading>>();
        foreach (var s in lot.GetSpots())
        {
            spotSensors[s.Id] = new Sensor<SpotSensorReading>($"SEN-SPOT-{s.Id}", "Ultrasonido", logger);
        }

        var sensorToSpot = new Dictionary<string, string>(hwConfig.BuildSensorToSpot());
        foreach (var s in spotSensors.Values)
            sensorToSpot[s.Id] = s.Id.Replace("SEN-SPOT-", "");

        var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
        bus.Subscribe<SensorReadingReceived>(handleReading.Handle);

        var spotToActuator = hwConfig.BuildSpotToActuator();
        var occupancyHandler = new SpotOccupancyChangedHandler(dispatcher, spotToActuator);

        foreach (var spot in lot.GetSpots())
            spot.OccupancyChanged += occupancyHandler.Handle;

        foreach (var spot in lot.GetSpots())
        {
            spot.OccupancyChanged += evt =>
            {
                _ = repository.UpdateSpotStatusAsync(evt.SpotId, evt.IsOccupied);
            };
        }

        ICapacityService capacityService = new CapacityService(lot, logger);
        IAlertService alertService = new AlertService(logger, repository);
        IAccessPolicy accessPolicy = new AlwaysAllowPolicy();
        var gateController = new GateController(capacityService, alertService, accessPolicy, logger);

        gateController.RegisterGate(ENTRY_GATE_ID, new Gate(ENTRY_GATE_ID, GateType.ENTRY, ENTRY_GATE_PIN, ENTRY_GATE_ACTUATOR_ID, dispatcher, logger));
        gateController.RegisterGate(EXIT_GATE_ID, new Gate(EXIT_GATE_ID, GateType.EXIT, EXIT_GATE_PIN, EXIT_GATE_ACTUATOR_ID, dispatcher, logger));

        IDisplay display = new LcdDisplay(dispatcher);
        var lcdCapacityHandler = new LcdCapacityHandler(lot, display);
        foreach (var spot in lot.GetSpots())
            spot.OccupancyChanged += lcdCapacityHandler.Handle;

        ILicensePlateRecognizer plateRecognizer = new PlaceholderPlateRecognizer();
        var gateSensorHandler = new GateSensorHandler(gateController, plateRecognizer, display, logger, hwConfig.BuildGateSensorMapping());
        bus.SubscribeAsync<SensorReadingReceived>(gateSensorHandler.HandleAsync, logger, "GateSensorHandler");

        bridge.StartListening();
        display.ShowCapacity(lot.AvailableSpots, lot.TotalSpots);

        var menu = new ConsoleMenu(lot, gateController, capacityService, repository, bus, spotSensors, gateSensor, bridge, dispatcher, consoleLogger, fileLogger);
        await menu.RunAsync();
    }
}