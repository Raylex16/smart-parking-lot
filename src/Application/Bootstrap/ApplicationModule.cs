using Microsoft.Extensions.DependencyInjection;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Application.Gates;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.Monitoring;
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Recognition;
using SmartParkingLot.Application.Sensors;
using SmartParkingLot.Application.Services;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;
using SmartParkingLot.Persistence;

namespace SmartParkingLot.Application.Bootstrap;

public sealed record ApplicationOptions(
    string ConfigPath,
    string ConnectionString,
    string LogsDir,
    string LotId,
    string EntryGateId,
    string ExitGateId,
    int EntryGatePin,
    int ExitGatePin,
    string EntryGateActuatorId,
    string ExitGateActuatorId,
    bool StartBridgeSafe = false,
    string? TessDataPath = null);

public sealed record ApplicationBootstrapResult(
    HardwareConfig HwConfig,
    IParkingRepository Repository,
    ParkingLot Lot);

public static class ApplicationModule
{
    public static async Task<ApplicationBootstrapResult> BootstrapAsync(
        ApplicationOptions opts,
        ILogger logger,
        CancellationToken ct = default)
    {
        var hwConfig = HardwareConfig.Load(opts.ConfigPath);

        var initializer = new DatabaseInitializer(opts.ConnectionString, logger);
        await initializer.InitializeAsync();

        IParkingRepository repository = new SqliteParkingRepository(opts.ConnectionString);

        var sync = new SyncHardwareConfigurationUseCase(hwConfig, repository, logger, opts.LotId);
        await sync.ExecuteAsync(ct);

        var lot = await repository.GetParkingLotByIdAsync(opts.LotId, ct)
            ?? throw new InvalidOperationException($"No se encontró el parqueadero '{opts.LotId}' en la BD.");

        return new ApplicationBootstrapResult(hwConfig, repository, lot);
    }

    public static IServiceCollection AddSmartParkingApplicationServices(
        this IServiceCollection services,
        ApplicationBootstrapResult bootstrap,
        ApplicationOptions opts,
        bool mockMode = false)
    {
        var (hwConfig, repository, lot) = bootstrap;

        services.AddSingleton(hwConfig);
        services.AddSingleton(lot);
        services.AddSingleton(repository);
        services.AddSingleton<IParkingRepository>(_ => repository);
        services.AddSingleton<IParkingLotRepository>(_ => repository);
        services.AddSingleton<ISpotRepository>(_ => repository);
        services.AddSingleton<IRequestRepository>(_ => repository);
        services.AddSingleton<ISensorRepository>(_ => repository);
        services.AddSingleton<IDeviceActionRepository>(_ => repository);
        services.AddSingleton<IAlertRepository>(_ => repository);

        services.AddSingleton<IEventPublisher, InProcessEventBus>();

        if (mockMode)
        {
            var sensorIds = hwConfig.Sensors.Select(s => s.SensorId).ToList();
            services.AddSingleton<MockArduinoBridge>(sp =>
                new MockArduinoBridge(
                    sp.GetRequiredService<IEventPublisher>(),
                    sp.GetRequiredService<ILogger>(),
                    sensorIds));
            services.AddSingleton<ArduinoSerialBridge>(sp => sp.GetRequiredService<MockArduinoBridge>());
            services.AddSingleton<IArduinoReader>(sp => sp.GetRequiredService<MockArduinoBridge>());
            services.AddSingleton<ISerialWriter>(sp => sp.GetRequiredService<ArduinoSerialBridge>());
            services.AddSingleton<SerialCommandDispatcher>(sp =>
                new SerialCommandDispatcher(sp.GetRequiredService<MockArduinoBridge>(), sp.GetRequiredService<ILogger>()));
            services.AddSingleton<ICommandDispatcher>(sp => sp.GetRequiredService<SerialCommandDispatcher>());
            services.AddSingleton<IHardwareStatus>(sp =>
                new MockHardwareStatus(sp.GetRequiredService<MockArduinoBridge>()));
        }
        else
        {
            services.AddSingleton<ArduinoSerialBridge>(sp =>
                new ArduinoSerialBridge(
                    hwConfig.Port, hwConfig.BaudRate,
                    sp.GetRequiredService<IEventPublisher>(),
                    sp.GetRequiredService<ILogger>()));
            services.AddSingleton<IArduinoReader>(sp => sp.GetRequiredService<ArduinoSerialBridge>());
            services.AddSingleton<ISerialWriter>(sp => sp.GetRequiredService<ArduinoSerialBridge>());
            services.AddSingleton<SerialCommandDispatcher>(sp =>
                new SerialCommandDispatcher(sp.GetRequiredService<ArduinoSerialBridge>(), sp.GetRequiredService<ILogger>()));
            services.AddSingleton<ICommandDispatcher>(sp => sp.GetRequiredService<SerialCommandDispatcher>());
            services.AddSingleton<IHardwareStatus>(sp =>
                new ArduinoHardwareStatus(sp.GetRequiredService<ArduinoSerialBridge>(), hwConfig.Port));
        }

        services.AddSingleton<Sensor<GateSensorReading>>(sp =>
            new Sensor<GateSensorReading>("SEN-GATE-01", "LPR", sp.GetRequiredService<ILogger>()));

        services.AddSingleton<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(sp =>
        {
            var log = sp.GetRequiredService<ILogger>();
            return lot.GetSpots()
                .ToDictionary(s => s.Id, s => new Sensor<SpotSensorReading>($"SEN-SPOT-{s.Id}", "Ultrasonido", log))
                as IReadOnlyDictionary<string, Sensor<SpotSensorReading>>;
        });

        services.AddSingleton<ICapacityService>(sp =>
            new CapacityService(lot, sp.GetRequiredService<ISpotRepository>(), sp.GetRequiredService<ILogger>()));

        services.AddSingleton<IAlertService>(sp =>
            new AlertService(sp.GetRequiredService<ILogger>(), sp.GetRequiredService<IAlertRepository>()));

        services.AddSingleton<IApprovalQueue, InMemoryApprovalQueue>();
        services.AddSingleton<IApprovalDecisionService>(sp =>
            new ApprovalDecisionService(sp.GetRequiredService<IApprovalQueue>()));

        var manualTimeout = TimeSpan.FromSeconds(hwConfig.ManualApprovalTimeoutSeconds);
        services.AddSingleton<SwitchableAccessPolicy>(sp =>
        {
            var queue   = sp.GetRequiredService<IApprovalQueue>();
            var logger  = sp.GetRequiredService<ILogger>();
            Func<ParkingMode, IAccessPolicy> factory = mode => mode switch
            {
                ParkingMode.MANUAL    => new ManualAccessPolicy(queue, logger, manualTimeout),
                ParkingMode.AUTOMATIC => new AlwaysAllowPolicy(),
                _                     => new AlwaysAllowPolicy()
            };
            return new SwitchableAccessPolicy(factory(lot.Mode));
        });
        services.AddSingleton<IAccessPolicy>(sp => sp.GetRequiredService<SwitchableAccessPolicy>());

        services.AddSingleton<IParkingModeService>(sp =>
        {
            var queue   = sp.GetRequiredService<IApprovalQueue>();
            var logger  = sp.GetRequiredService<ILogger>();
            var policy  = sp.GetRequiredService<SwitchableAccessPolicy>();
            Func<ParkingMode, IAccessPolicy> factory = mode => mode switch
            {
                ParkingMode.MANUAL    => new ManualAccessPolicy(queue, logger, manualTimeout),
                ParkingMode.AUTOMATIC => new AlwaysAllowPolicy(),
                _                     => new AlwaysAllowPolicy()
            };
            return new ParkingModeService(lot, policy, repository, logger, factory);
        });

        services.AddSingleton<GateController>(sp =>
        {
            var gc = new GateController(
                sp.GetRequiredService<ICapacityService>(),
                sp.GetRequiredService<IAlertService>(),
                sp.GetRequiredService<IAccessPolicy>(),
                sp.GetRequiredService<ILogger>());
            var disp = sp.GetRequiredService<SerialCommandDispatcher>();
            var log  = sp.GetRequiredService<ILogger>();
            gc.RegisterGate(opts.EntryGateId,
                new Gate(opts.EntryGateId, GateType.ENTRY, opts.EntryGatePin, opts.EntryGateActuatorId, disp, log));
            gc.RegisterGate(opts.ExitGateId,
                new Gate(opts.ExitGateId, GateType.EXIT, opts.ExitGatePin, opts.ExitGateActuatorId, disp, log));
            return gc;
        });

        var gateTypes = hwConfig.Gates.ToDictionary(g => g.GateId, g => g.Type);
        services.AddSingleton<IGateOperationsService>(sp =>
            new GateOperationsService(
                sp.GetRequiredService<GateController>(),
                repository,
                lot,
                sp.GetRequiredService<Sensor<GateSensorReading>>(),
                gateTypes));

        services.AddSingleton<IManualSensorService>(sp =>
            new ManualSensorService(
                sp.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(),
                sp.GetRequiredService<Sensor<GateSensorReading>>(),
                sp.GetRequiredService<IEventPublisher>(),
                repository,
                hwConfig.BuildGateToIrSensorMapping()));

        services.AddSingleton<IArduinoMonitoringService>(sp =>
            new ArduinoMonitoringService(sp.GetRequiredService<IArduinoReader>()));

        services.AddSingleton<IHardwareConfigurationService>(sp =>
            new HardwareConfigurationService(
                hwConfig,
                sp.GetRequiredService<IHardwareStatus>(),
                sp.GetRequiredService<Sensor<GateSensorReading>>(),
                sp.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>()));

        services.AddSingleton<IAvailableSerialPortsQuery, AvailableSerialPortsQuery>();

        services.AddSingleton<GateTypeRegistry>(sp =>
        {
            var gc = sp.GetRequiredService<GateController>();
            var map = gc.GetRegisteredGates()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetType().Name);
            return new GateTypeRegistry(map);
        });

        services.AddSingleton<IGetLotSnapshotQuery>(sp =>
            new GetLotSnapshotQuery(
                repository,
                sp.GetRequiredService<GateController>(),
                sp.GetRequiredService<GateTypeRegistry>(),
                opts.LotId));

        services.AddSingleton<ILotSnapshotStream, LotSnapshotStream>();
        services.AddTransient<IGetSpotRowsQuery, GetSpotRowsQuery>();
        services.AddTransient<IGetSensorReadingsQuery, GetSensorReadingsQuery>();
        services.AddTransient<ILogQueryService>(sp =>
            new LogQueryService(repository, sp.GetRequiredService<Logging.FileLogger>()));

        if (!string.IsNullOrEmpty(opts.TessDataPath))
        {
            services.AddSingleton<ICameraCapture>(sp =>
                new OV7670FrameReader(
                    sp.GetRequiredService<ISerialWriter>(),
                    sp.GetRequiredService<IEventPublisher>()));
            services.AddSingleton<ILicensePlateRecognizer>(sp =>
                new TesseractPlateRecognizer(
                    sp.GetRequiredService<ICameraCapture>(),
                    opts.TessDataPath));
        }
        else
        {
            services.AddSingleton<ILicensePlateRecognizer, PlaceholderPlateRecognizer>();
        }

        services.AddSingleton<IApplicationStartup>(sp =>
            new ApplicationStartup(
                lot,
                hwConfig,
                sp.GetRequiredService<IEventPublisher>(),
                sp.GetRequiredService<IArduinoReader>(),
                sp.GetRequiredService<ICommandDispatcher>(),
                sp.GetRequiredService<IReadOnlyDictionary<string, Sensor<SpotSensorReading>>>(),
                sp.GetRequiredService<IGateOperationsService>(),
                sp.GetRequiredService<ILicensePlateRecognizer>(),
                repository,
                sp.GetRequiredService<ILogger>(),
                opts.StartBridgeSafe));

        return services;
    }
}
