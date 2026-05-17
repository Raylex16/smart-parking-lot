using Microsoft.Extensions.DependencyInjection;
using SmartParkingLot.Application.Bootstrap;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Application.Gates;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Application.Monitoring;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Sensors;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Cli;

public sealed class ParkingLotApp
{
    public async Task RunAsync()
    {
        var baseDir    = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "hardware.json");
        var dbPath     = Path.Combine(baseDir, DB_FOLDER_NAME, DB_FILE_NAME);
        var logsDir    = Path.Combine(baseDir, "logs");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var consoleLogger = new ConsoleLogger(LogLevel.Info);
        var fileLogger    = new FileLogger(logsDir, LogLevel.Debug);
        ILogger logger    = new CompositeLogger(consoleLogger, fileLogger);

        var opts = new ApplicationOptions(
            ConfigPath:            configPath,
            ConnectionString:      $"Data Source={dbPath};",
            LogsDir:               logsDir,
            LotId:                 DEFAULT_LOT_ID,
            EntryGateId:           ENTRY_GATE_ID,
            ExitGateId:            EXIT_GATE_ID,
            EntryGatePin:          ENTRY_GATE_PIN,
            ExitGatePin:           EXIT_GATE_PIN,
            EntryGateActuatorId:   ENTRY_GATE_ACTUATOR_ID,
            ExitGateActuatorId:    EXIT_GATE_ACTUATOR_ID,
            StartBridgeSafe:       false);

        var bootstrap = await ApplicationModule.BootstrapAsync(opts, logger);

        var services = new ServiceCollection();
        services.AddSingleton(consoleLogger);
        services.AddSingleton(fileLogger);
        services.AddSingleton<ILogger>(logger);
        services.AddSmartParkingApplicationServices(bootstrap, opts, mockMode: bootstrap.HwConfig.Port == "MOCK");

        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IApplicationStartup>().StartAsync();

        var menu = new ConsoleMenu(
            provider.GetRequiredService<ILotSnapshotStream>(),
            provider.GetRequiredService<IGetSpotRowsQuery>(),
            provider.GetRequiredService<IGetSensorReadingsQuery>(),
            provider.GetRequiredService<IManualSensorService>(),
            provider.GetRequiredService<IGateOperationsService>(),
            provider.GetRequiredService<IApprovalDecisionService>(),
            provider.GetRequiredService<IArduinoMonitoringService>(),
            provider.GetRequiredService<IParkingModeService>(),
            provider.GetRequiredService<ILogQueryService>(),
            consoleLogger);

        await menu.RunAsync();
    }
}
