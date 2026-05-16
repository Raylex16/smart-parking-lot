using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmartParkingLot.Application;
using SmartParkingLot.Application.Display;
using SmartParkingLot.Application.Hardware;
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

namespace SmartParkingLot.Gui.Bootstrap;

/// Headless mirror of ParkingLotApp.RunAsync — wires the full domain
/// stack and exposes it to the GUI pages without a console loop.
/// <remarks>
/// DEPRECATED: Use <see cref="ServiceCollectionExtensions.BuildParkingServiceProviderAsync"/>
/// and resolve services via App.Services. Kept only as a compatibility shim
/// while pages are progressively migrated to ParkingServicesFacade.
/// </remarks>
[Obsolete("Use ServiceCollectionExtensions.BuildParkingServiceProviderAsync() and resolve services from App.Services. This class will be removed in a future iteration.")]
public sealed class ParkingServices : IDisposable
{
    public ParkingLot Lot { get; }
    public GateController GateController { get; }
    public ICapacityService CapacityService { get; }
    public IAlertService AlertService { get; }
    public IParkingRepository Repository { get; }
    public IEventPublisher Bus { get; }
    public IReadOnlyDictionary<string, Sensor<SpotSensorReading>> SpotSensors { get; }
    public Sensor<GateSensorReading> GateSensor { get; }
    public ArduinoSerialBridge Bridge { get; }
    public IHardwareStatus HardwareStatus { get; }
    public SerialCommandDispatcher Dispatcher { get; }
    public GuiLogger UiLogger { get; }
    public FileLogger FileLogger { get; }
    public HardwareConfig Config { get; }

    private ParkingServices(
        ParkingLot lot,
        GateController gateController,
        ICapacityService capacityService,
        IAlertService alertService,
        IParkingRepository repository,
        IEventPublisher bus,
        IReadOnlyDictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        ArduinoSerialBridge bridge,
        IHardwareStatus hardwareStatus,
        SerialCommandDispatcher dispatcher,
        GuiLogger uiLogger,
        FileLogger fileLogger,
        HardwareConfig config)
    {
        Lot = lot;
        GateController = gateController;
        CapacityService = capacityService;
        AlertService = alertService;
        Repository = repository;
        Bus = bus;
        SpotSensors = spotSensors;
        GateSensor = gateSensor;
        Bridge = bridge;
        HardwareStatus = hardwareStatus;
        Dispatcher = dispatcher;
        UiLogger = uiLogger;
        FileLogger = fileLogger;
        Config = config;
    }

    public static async Task<ParkingServices> BootstrapAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "hardware.json");
        var hwConfig = HardwareConfig.Load(configPath);

        var dbPath = Path.Combine(baseDir, GuiConstants.DB_FOLDER_NAME, GuiConstants.DB_FILE_NAME);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var logsDir = Path.Combine(baseDir, "logs");
        var uiLogger = new GuiLogger { MinimumLevel = LogLevel.Info };
        var fileLogger = new FileLogger(logsDir, LogLevel.Debug);
        ILogger logger = new CompositeLogger(uiLogger, fileLogger);

        var connectionString = $"Data Source={dbPath};";

        var initializer = new DatabaseInitializer(connectionString, logger);
        await initializer.InitializeAsync();

        IParkingRepository repository = new SqliteParkingRepository(connectionString);

        foreach (var mapping in hwConfig.Sensors)
            await repository.EnsureSpotExistsAsync(
                mapping.SpotId, GuiConstants.DEFAULT_LOT_ID,
                mapping.Address, mapping.Type, mapping.Floor);

        var validSpotIds = hwConfig.Sensors.Select(m => m.SpotId).ToList();
        var removed = await repository.RemoveOrphanSpotsAsync(GuiConstants.DEFAULT_LOT_ID, validSpotIds);
        if (removed > 0)
            logger.Info("ParkingServices", $"Eliminados {removed} spot(s) huérfanos no presentes en hardware.json");

        var lot = await repository.GetParkingLotByIdAsync(GuiConstants.DEFAULT_LOT_ID)
                  ?? throw new InvalidOperationException(
                      $"No se encontró el parqueadero '{GuiConstants.DEFAULT_LOT_ID}' en la BD.");

        IEventPublisher bus = new InProcessEventBus();

        var bridge = new ArduinoSerialBridge(hwConfig.Port, hwConfig.BaudRate, bus, logger);
        var hardwareStatus = new ArduinoHardwareStatus(bridge, hwConfig.Port);
        var dispatcher = new SerialCommandDispatcher(bridge, logger);

        var gateSensor = new Sensor<GateSensorReading>("SEN-GATE-01", "LPR", logger);

        var spotSensors = new Dictionary<string, Sensor<SpotSensorReading>>();
        foreach (var s in lot.GetSpots())
            spotSensors[s.Id] = new Sensor<SpotSensorReading>($"SEN-SPOT-{s.Id}", "Ultrasonido", logger);

        var sensorToSpot = new Dictionary<string, string>(hwConfig.BuildSensorToSpot());
        foreach (var s in spotSensors.Values)
            sensorToSpot[s.Id] = s.Id.Replace("SEN-SPOT-", "");

        var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
        bus.Subscribe<SensorReadingReceived>(handleReading.Handle);

        var spotToActuator = hwConfig.BuildSpotToActuator();
        var occupancyHandler = new SpotOccupancyChangedHandler(dispatcher, spotToActuator);

        foreach (var spot in lot.GetSpots())
        {
            spot.OccupancyChanged += occupancyHandler.Handle;
            spot.OccupancyChanged += evt =>
            {
                _ = repository.UpdateSpotStatusAsync(evt.SpotId, evt.IsOccupied);
            };
        }

        ICapacityService capacityService = new CapacityService(lot, logger);
        IAlertService alertService = new AlertService(logger, repository);
        IAccessPolicy accessPolicy = new AlwaysAllowPolicy();
        var gateController = new GateController(capacityService, alertService, accessPolicy, logger);

        gateController.RegisterGate(GuiConstants.ENTRY_GATE_ID,
            new Gate(GuiConstants.ENTRY_GATE_ID, GateType.ENTRY, GuiConstants.ENTRY_GATE_PIN,
                     GuiConstants.ENTRY_GATE_ACTUATOR_ID, dispatcher, logger));
        gateController.RegisterGate(GuiConstants.EXIT_GATE_ID,
            new Gate(GuiConstants.EXIT_GATE_ID, GateType.EXIT, GuiConstants.EXIT_GATE_PIN,
                     GuiConstants.EXIT_GATE_ACTUATOR_ID, dispatcher, logger));

        IDisplay display = new LcdDisplay(dispatcher);
        var lcdCapacityHandler = new LcdCapacityHandler(lot, display);
        foreach (var spot in lot.GetSpots())
            spot.OccupancyChanged += lcdCapacityHandler.Handle;

        ILicensePlateRecognizer plateRecognizer = new PlaceholderPlateRecognizer();
        var gateSensorHandler = new GateSensorHandler(gateController, plateRecognizer, display, logger,
            hwConfig.BuildGateSensorMapping());
        bus.SubscribeAsync<SensorReadingReceived>(gateSensorHandler.HandleAsync, logger, "GateSensorHandler");

        try
        {
            bridge.StartListening();
        }
        catch (Exception ex)
        {
            logger.Warn("ParkingServices",
                $"Arduino no disponible al iniciar — la GUI seguirá funcionando offline. ({ex.Message})");
        }

        display.ShowCapacity(lot.AvailableSpots, lot.TotalSpots);

        return new ParkingServices(lot, gateController, capacityService, alertService, repository, bus,
            spotSensors, gateSensor, bridge, hardwareStatus, dispatcher, uiLogger, fileLogger, hwConfig);
    }

    public void Dispose()
    {
        try { Bridge.StopListening(); } catch { }
        Dispatcher.Dispose();
        Bridge.Dispose();
        (HardwareStatus as IDisposable)?.Dispose();
    }
}
