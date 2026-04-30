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

public sealed class ParkingLotApp
{
    public async Task RunAsync()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "hardware.json");
        var hwConfig = HardwareConfig.Load(configPath);

        var dbPath = Path.Combine(AppContext.BaseDirectory, DB_FOLDER_NAME, DB_FILE_NAME);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var connectionString = $"Data Source={dbPath};";

        var initializer = new DatabaseInitializer(connectionString);
        await initializer.InitializeAsync();

        IParkingRepository repository = new SqliteParkingRepository(connectionString);

        foreach (var mapping in hwConfig.Sensors)
            await repository.EnsureSpotExistsAsync(
                mapping.SpotId, DEFAULT_LOT_ID,
                mapping.Address, mapping.Type, mapping.Floor);

        var lot = await repository.GetParkingLotByIdAsync(DEFAULT_LOT_ID);
        if (lot is null)
        {
            Console.WriteLine($"[ERROR] No se encontró el parqueadero '{DEFAULT_LOT_ID}' en la BD.");
            return;
        }

        IEventPublisher bus = new InProcessEventBus();

        using var bridge = new ArduinoSerialBridge(hwConfig.Port, hwConfig.BaudRate, bus);
        using var dispatcher = new SerialCommandDispatcher(bridge);

        var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "LPR");

        var spotSensors = new Dictionary<string, Sensor<SpotSensorReading>>();
        foreach (var s in lot.GetSpots())
        {
            spotSensors[s.Id] = new Sensor<SpotSensorReading>($"SEN-SPOT-{s.Id}", "Ultrasonido");
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

        ICapacityService capacityService = new CapacityService(lot);
        IAlertService alertService = new AlertService();
        var gateController = new GateController(capacityService, alertService);

        gateController.RegisterGate(ENTRY_GATE_ID, new Gate(ENTRY_GATE_ID, GateType.ENTRY, ENTRY_GATE_PIN));
        gateController.RegisterGate(EXIT_GATE_ID, new Gate(EXIT_GATE_ID, GateType.EXIT, EXIT_GATE_PIN));

        bridge.ConsoleLoggingEnabled = false;
        bridge.StartListening();

        var menu = new ConsoleMenu(lot, gateController, capacityService, repository, bus, spotSensors, gateSensor, bridge, dispatcher);
        await menu.RunAsync();
    }
}
