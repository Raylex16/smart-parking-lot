using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartParkingLot.Application;
using SmartParkingLot.Application.Display;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Recognition;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Gui.Infrastructure;
using SmartParkingLot.Gui.ViewModels;
using Microsoft.UI.Dispatching;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;
using SmartParkingLot.Persistence;

namespace SmartParkingLot.Gui.Bootstrap;

/// <summary>
/// Extension methods that decompose the former ParkingServices god-object
/// into focused IServiceCollection registrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    // ---------------------------------------------------------------
    // Granular extension methods (architectural markers / reusable)
    // ---------------------------------------------------------------

    /// <summary>Registers ParkingLot singleton. Requires lot to be pre-built.</summary>
    public static IServiceCollection AddDomain(
        this IServiceCollection services, ParkingLot lot)
    {
        services.AddSingleton(lot);
        return services;
    }

    /// <summary>Registers IParkingRepository using SQLite.</summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IParkingRepository>(
            _ => new SqliteParkingRepository(connectionString));
        return services;
    }

    /// <summary>Registers HardwareConfig, ArduinoSerialBridge (or MockArduinoBridge),
    /// SerialCommandDispatcher, and IHardwareStatus.
    /// When <paramref name="config"/>.Port is <c>"MOCK"</c> no serial port is opened.</summary>
    public static IServiceCollection AddHardware(
        this IServiceCollection services, HardwareConfig config)
    {
        services.AddSingleton(config);

        if (config.Port == "MOCK")
        {
            var sensorIds = config.Sensors.Select(s => s.SensorId).ToList();

            services.AddSingleton<MockArduinoBridge>(sp =>
                new MockArduinoBridge(
                    sp.GetRequiredService<IEventPublisher>(),
                    sp.GetRequiredService<ILogger>(),
                    sensorIds));

            services.AddSingleton<ArduinoSerialBridge>(sp =>
                sp.GetRequiredService<MockArduinoBridge>());

            services.AddSingleton<SerialCommandDispatcher>(sp =>
                new SerialCommandDispatcher(
                    sp.GetRequiredService<MockArduinoBridge>(),
                    sp.GetRequiredService<ILogger>()));

            services.AddSingleton<IHardwareStatus>(sp =>
                new MockHardwareStatus(
                    sp.GetRequiredService<MockArduinoBridge>()));
        }
        else
        {
            services.AddSingleton<ArduinoSerialBridge>(sp =>
                new ArduinoSerialBridge(
                    config.Port, config.BaudRate,
                    sp.GetRequiredService<IEventPublisher>(),
                    sp.GetRequiredService<ILogger>()));

            services.AddSingleton<SerialCommandDispatcher>(sp =>
                new SerialCommandDispatcher(
                    sp.GetRequiredService<ArduinoSerialBridge>(),
                    sp.GetRequiredService<ILogger>()));

            services.AddSingleton<IHardwareStatus>(sp =>
                new ArduinoHardwareStatus(
                    sp.GetRequiredService<ArduinoSerialBridge>(),
                    config.Port));
        }

        return services;
    }

    /// <summary>Registers IEventPublisher, GuiLogger, FileLogger, ILogger,
    /// ICapacityService, IAlertService, IAccessPolicy, and GateController.</summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IEventPublisher, InProcessEventBus>();

        services.AddSingleton<GuiLogger>(
            _ => new GuiLogger { MinimumLevel = LogLevel.Info });

        services.AddSingleton<FileLogger>(sp =>
        {
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            return new FileLogger(logsDir, LogLevel.Debug);
        });

        services.AddSingleton<ILogger>(sp =>
            new CompositeLogger(
                sp.GetRequiredService<GuiLogger>(),
                sp.GetRequiredService<FileLogger>()));

        services.AddSingleton<ICapacityService>(sp =>
            new CapacityService(
                sp.GetRequiredService<ParkingLot>(),
                sp.GetRequiredService<ILogger>()));

        services.AddSingleton<IAlertService>(sp =>
            new AlertService(
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IParkingRepository>()));

        services.AddSingleton<IAccessPolicy, AlwaysAllowPolicy>();

        services.AddSingleton<GateController>(sp =>
            new GateController(
                sp.GetRequiredService<ICapacityService>(),
                sp.GetRequiredService<IAlertService>(),
                sp.GetRequiredService<IAccessPolicy>(),
                sp.GetRequiredService<ILogger>()));

        services.AddSingleton<GateTypeRegistry>(sp =>
        {
            // Build a gateId → type-name map from the registered gates
            var gc = sp.GetRequiredService<GateController>();
            var map = gc.GetRegisteredGates()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetType().Name);
            return new GateTypeRegistry(map);
        });

        services.AddSingleton<IGetLotSnapshotQuery>(sp =>
            new GetLotSnapshotQuery(
                sp.GetRequiredService<IParkingRepository>(),
                sp.GetRequiredService<GateController>(),
                sp.GetRequiredService<GateTypeRegistry>(),
                GuiConstants.DEFAULT_LOT_ID));

        services.AddSingleton<ILotSnapshotStream, LotSnapshotStream>();

        services.AddSingleton<IUiThreadDispatcher>(sp =>
            new DispatcherQueueUiThreadDispatcher(
                DispatcherQueue.GetForCurrentThread()));

        services.AddTransient<IGetSpotRowsQuery, GetSpotRowsQuery>();

        return services;
    }

    /// <summary>Registers all GUI pages as Transient.
    /// Each page still receives a ParkingServicesFacade (adapter) so existing
    /// page constructors compile without modification. Other agents can refactor
    /// individual pages to accept granular dependencies as needed.</summary>
    public static IServiceCollection AddGuiViewModels(
        this IServiceCollection services)
    {
        // Thin adapter: wraps the individual singletons into the shape pages
        // currently expect. This avoids touching page code while eliminating
        // ParkingServices as an injection point.
        services.AddSingleton<ParkingServicesFacade>(sp =>
            new ParkingServicesFacade(
                sp.GetRequiredService<ParkingLot>(),
                sp.GetRequiredService<GateController>(),
                sp.GetRequiredService<ICapacityService>(),
                sp.GetRequiredService<IAlertService>(),
                sp.GetRequiredService<IParkingRepository>(),
                sp.GetRequiredService<IEventPublisher>(),
                sp.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(),
                sp.GetRequiredService<Sensor<GateSensorReading>>(),
                sp.GetRequiredService<ArduinoSerialBridge>(),
                sp.GetRequiredService<IHardwareStatus>(),
                sp.GetRequiredService<SerialCommandDispatcher>(),
                sp.GetRequiredService<GuiLogger>(),
                sp.GetRequiredService<FileLogger>(),
                sp.GetRequiredService<HardwareConfig>()));

        services.AddTransient<DashboardViewModel>(sp =>
            new DashboardViewModel(
                sp.GetRequiredService<ILotSnapshotStream>(),
                sp.GetRequiredService<IUiThreadDispatcher>(),
                sp.GetRequiredService<GuiLogger>()));

        services.AddTransient<MapPageViewModel>(sp =>
            new MapPageViewModel(
                sp.GetRequiredService<ILotSnapshotStream>(),
                sp.GetRequiredService<IUiThreadDispatcher>(),
                sp.GetRequiredService<IParkingRepository>(),
                sp.GetRequiredService<IEventPublisher>(),
                sp.GetRequiredService<GateController>(),
                sp.GetRequiredService<ParkingLot>(),
                sp.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(),
                sp.GetRequiredService<Sensor<GateSensorReading>>(),
                sp.GetRequiredService<HardwareConfig>()));

        services.AddTransient<Pages.DashboardPage>(sp =>
            new Pages.DashboardPage(sp.GetRequiredService<DashboardViewModel>()));
        services.AddTransient<Pages.MapPage>(sp =>
            new Pages.MapPage(sp.GetRequiredService<MapPageViewModel>()));
        services.AddTransient<LogPageViewModel>(sp =>
            new LogPageViewModel(
                sp.GetRequiredService<IParkingRepository>()));
        services.AddTransient<Pages.LogPage>(sp =>
            new Pages.LogPage(sp.GetRequiredService<LogPageViewModel>()));

        services.AddTransient<AdminPageViewModel>(sp =>
            new AdminPageViewModel(
                sp.GetRequiredService<IParkingRepository>(),
                sp.GetRequiredService<ParkingLot>()));
        services.AddTransient<Pages.AdminPage>(sp =>
            new Pages.AdminPage(sp.GetRequiredService<AdminPageViewModel>()));

        services.AddTransient<HardwarePageViewModel>(sp =>
            new HardwarePageViewModel(
                sp.GetRequiredService<IHardwareStatus>(),
                sp.GetRequiredService<IEventPublisher>(),
                sp.GetRequiredService<GuiLogger>(),
                sp.GetRequiredService<FileLogger>(),
                sp.GetRequiredService<ArduinoSerialBridge>(),
                sp.GetRequiredService<HardwareConfig>(),
                sp.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(),
                sp.GetRequiredService<Sensor<GateSensorReading>>(),
                sp.GetRequiredService<IUiThreadDispatcher>()));
        services.AddTransient<Pages.HardwarePage>(sp =>
            new Pages.HardwarePage(sp.GetRequiredService<HardwarePageViewModel>()));

        return services;
    }

    // ---------------------------------------------------------------
    // Full async bootstrap entry point
    // ---------------------------------------------------------------

    /// <summary>
    /// Runs async side-effects (DB init, hardware seeding) and returns
    /// a fully-wired IServiceProvider. Called once from App.OnLaunched.
    /// </summary>
    public static async Task<IServiceProvider> BuildParkingServiceProviderAsync()
    {
        var baseDir    = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "hardware.json");
        var hwConfig   = HardwareConfig.Load(configPath);

        var dbPath = Path.Combine(baseDir,
            GuiConstants.DB_FOLDER_NAME, GuiConstants.DB_FILE_NAME);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var connectionString = $"Data Source={dbPath};";

        // ── Phase 1: async work that must precede DI container build ──
        var logsDir    = Path.Combine(baseDir, "logs");
        var uiLogger   = new GuiLogger { MinimumLevel = LogLevel.Info };
        var fileLogger = new FileLogger(logsDir, LogLevel.Debug);
        ILogger logger = new CompositeLogger(uiLogger, fileLogger);

        var initializer = new DatabaseInitializer(connectionString, logger);
        await initializer.InitializeAsync();

        IParkingRepository repository = new SqliteParkingRepository(connectionString);

        foreach (var mapping in hwConfig.Sensors)
            await repository.EnsureSpotExistsAsync(
                mapping.SpotId, GuiConstants.DEFAULT_LOT_ID,
                mapping.Address, mapping.Type, mapping.Floor);

        var validSpotIds = hwConfig.Sensors.Select(m => m.SpotId).ToList();
        var removed = await repository.RemoveOrphanSpotsAsync(
            GuiConstants.DEFAULT_LOT_ID, validSpotIds);
        if (removed > 0)
            logger.Info("Bootstrap",
                $"Eliminados {removed} spot(s) huérfanos no presentes en hardware.json");

        var lot = await repository.GetParkingLotByIdAsync(GuiConstants.DEFAULT_LOT_ID)
                  ?? throw new InvalidOperationException(
                      $"No se encontró el parqueadero '{GuiConstants.DEFAULT_LOT_ID}' en la BD.");

        // ── Phase 2: build the DI container ──
        var services = new ServiceCollection();

        // Pre-built singletons from Phase 1
        services.AddSingleton(uiLogger);
        services.AddSingleton(fileLogger);
        services.AddSingleton<ILogger>(logger);
        services.AddSingleton<IParkingRepository>(repository);
        services.AddSingleton(hwConfig);
        services.AddSingleton(lot);

        // Event bus
        services.AddSingleton<IEventPublisher, InProcessEventBus>();

        // Hardware layer
        if (hwConfig.Port == "MOCK")
        {
            // Mock mode: no physical serial port is opened.
            // MockArduinoBridge inherits ArduinoSerialBridge so it satisfies every
            // DI registration that expects the concrete type (ViewModels, Facade).
            var sensorIds = hwConfig.Sensors.Select(s => s.SensorId).ToList();

            services.AddSingleton<MockArduinoBridge>(sp =>
                new MockArduinoBridge(
                    sp.GetRequiredService<IEventPublisher>(),
                    sp.GetRequiredService<ILogger>(),
                    sensorIds));

            // Register as the concrete base type so existing code resolves it.
            services.AddSingleton<ArduinoSerialBridge>(sp =>
                sp.GetRequiredService<MockArduinoBridge>());

            services.AddSingleton<SerialCommandDispatcher>(sp =>
                new SerialCommandDispatcher(
                    sp.GetRequiredService<MockArduinoBridge>(),
                    sp.GetRequiredService<ILogger>()));

            services.AddSingleton<IHardwareStatus>(sp =>
                new MockHardwareStatus(
                    sp.GetRequiredService<MockArduinoBridge>()));
        }
        else
        {
            // Real hardware path — unchanged.
            services.AddSingleton<ArduinoSerialBridge>(sp =>
                new ArduinoSerialBridge(
                    hwConfig.Port, hwConfig.BaudRate,
                    sp.GetRequiredService<IEventPublisher>(),
                    sp.GetRequiredService<ILogger>()));

            services.AddSingleton<SerialCommandDispatcher>(sp =>
                new SerialCommandDispatcher(
                    sp.GetRequiredService<ArduinoSerialBridge>(),
                    sp.GetRequiredService<ILogger>()));

            services.AddSingleton<IHardwareStatus>(sp =>
                new ArduinoHardwareStatus(
                    sp.GetRequiredService<ArduinoSerialBridge>(),
                    hwConfig.Port));
        }

        // Sensors
        services.AddSingleton<Sensor<GateSensorReading>>(sp =>
            new Sensor<GateSensorReading>(
                "SEN-GATE-01", "LPR", sp.GetRequiredService<ILogger>()));

        services.AddSingleton<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(sp =>
        {
            var log  = sp.GetRequiredService<ILogger>();
            var dict = new Dictionary<string, Sensor<SpotSensorReading>>();
            foreach (var s in lot.GetSpots())
                dict[s.Id] = new Sensor<SpotSensorReading>(
                    $"SEN-SPOT-{s.Id}", "Ultrasonido", log);
            return dict;
        });

        // Application services
        services.AddSingleton<ICapacityService>(sp =>
            new CapacityService(
                sp.GetRequiredService<ParkingLot>(),
                sp.GetRequiredService<ILogger>()));

        services.AddSingleton<IAlertService>(sp =>
            new AlertService(
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IParkingRepository>()));

        services.AddSingleton<IAccessPolicy, AlwaysAllowPolicy>();

        // GateController with gates wired
        services.AddSingleton<GateController>(sp =>
        {
            var capacity   = sp.GetRequiredService<ICapacityService>();
            var alert      = sp.GetRequiredService<IAlertService>();
            var policy     = sp.GetRequiredService<IAccessPolicy>();
            var log        = sp.GetRequiredService<ILogger>();
            var dispatcher = sp.GetRequiredService<SerialCommandDispatcher>();

            var gc = new GateController(capacity, alert, policy, log);
            gc.RegisterGate(GuiConstants.ENTRY_GATE_ID,
                new Gate(GuiConstants.ENTRY_GATE_ID, GateType.ENTRY,
                         GuiConstants.ENTRY_GATE_PIN,
                         GuiConstants.ENTRY_GATE_ACTUATOR_ID, dispatcher, log));
            gc.RegisterGate(GuiConstants.EXIT_GATE_ID,
                new Gate(GuiConstants.EXIT_GATE_ID, GateType.EXIT,
                         GuiConstants.EXIT_GATE_PIN,
                         GuiConstants.EXIT_GATE_ACTUATOR_ID, dispatcher, log));
            return gc;
        });

        // Pages
        services.AddGuiViewModels();

        var provider = services.BuildServiceProvider();

        // ── Phase 3: side-effectful wiring (event subscriptions, hardware start) ──
        var bus          = provider.GetRequiredService<IEventPublisher>();
        var dispatcher   = provider.GetRequiredService<SerialCommandDispatcher>();
        var spotSensors  = provider.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>();
        var bridge       = provider.GetRequiredService<ArduinoSerialBridge>();
        var gc           = provider.GetRequiredService<GateController>();
        var gateSensor   = provider.GetRequiredService<Sensor<GateSensorReading>>();
        var logSvc       = provider.GetRequiredService<ILogger>();
        var repoSvc      = provider.GetRequiredService<IParkingRepository>();

        // Wire spot-sensor → use-case → bus
        var sensorToSpot = new Dictionary<string, string>(hwConfig.BuildSensorToSpot());
        foreach (var s in spotSensors.Values)
            sensorToSpot[s.Id] = s.Id.Replace("SEN-SPOT-", "");
        var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
        bus.Subscribe<SensorReadingReceived>(handleReading.Handle);

        // Wire occupancy events
        var spotToActuator   = hwConfig.BuildSpotToActuator();
        var occupancyHandler = new SpotOccupancyChangedHandler(dispatcher, spotToActuator);
        foreach (var spot in lot.GetSpots())
        {
            spot.OccupancyChanged += occupancyHandler.Handle;
            spot.OccupancyChanged += evt =>
            {
                _ = repoSvc.UpdateSpotStatusAsync(evt.SpotId, evt.IsOccupied);
            };
        }

        // LCD display
        IDisplay display    = new LcdDisplay(dispatcher);
        var lcdHandler      = new LcdCapacityHandler(lot, display);
        foreach (var spot in lot.GetSpots())
            spot.OccupancyChanged += lcdHandler.Handle;

        // Gate sensor handler
        ILicensePlateRecognizer plateRecognizer = new PlaceholderPlateRecognizer();
        var gateSensorHandler = new GateSensorHandler(
            gc, plateRecognizer, display, logSvc,
            hwConfig.BuildGateSensorMapping());
        bus.SubscribeAsync<SensorReadingReceived>(
            gateSensorHandler.HandleAsync, logSvc, "GateSensorHandler");

        // Start hardware bridge (mock mode is always safe; real port is non-fatal)
        if (hwConfig.Port == "MOCK")
        {
            bridge.StartListening();
        }
        else
        {
            try
            {
                bridge.StartListening();
            }
            catch (Exception ex)
            {
                logSvc.Warn("Bootstrap",
                    $"Arduino no disponible al iniciar — la GUI seguirá funcionando offline. ({ex.Message})");
            }
        }

        display.ShowCapacity(lot.AvailableSpots, lot.TotalSpots);

        return provider;
    }
}
