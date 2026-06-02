using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using SmartParkingLot.Application.Bootstrap;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Application.Gates;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Application.Monitoring;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Sensors;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Infrastructure;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Bootstrap;

public static class ServiceCollectionExtensions
{
    public static async Task<IServiceProvider> BuildParkingServiceProviderAsync()
    {
        var baseDir    = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "hardware.json");
        var dbPath     = Path.Combine(baseDir, GuiConstants.DB_FOLDER_NAME, GuiConstants.DB_FILE_NAME);
        var logsDir    = Path.Combine(baseDir, "logs");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var uiLogger   = new GuiLogger { MinimumLevel = LogLevel.Info };
        var fileLogger = new FileLogger(logsDir, LogLevel.Debug);
        ILogger logger = new CompositeLogger(uiLogger, fileLogger);

        var opts = new ApplicationOptions(
            ConfigPath:          configPath,
            ConnectionString:    $"Data Source={dbPath};",
            LogsDir:             logsDir,
            LotId:               GuiConstants.DEFAULT_LOT_ID,
            EntryGateId:         GuiConstants.ENTRY_GATE_ID,
            ExitGateId:          GuiConstants.EXIT_GATE_ID,
            EntryGatePin:        GuiConstants.ENTRY_GATE_PIN,
            ExitGatePin:         GuiConstants.EXIT_GATE_PIN,
            EntryGateActuatorId: GuiConstants.ENTRY_GATE_ACTUATOR_ID,
            ExitGateActuatorId:  GuiConstants.EXIT_GATE_ACTUATOR_ID,
            StartBridgeSafe:     true);

        var bootstrap = await ApplicationModule.BootstrapAsync(opts, logger);

        var services = new ServiceCollection();

        services.AddSingleton(uiLogger);
        services.AddSingleton(fileLogger);
        services.AddSingleton<ILogger>(logger);

        bool mockMode = bootstrap.HwConfig.Port == "MOCK";
        services.AddSmartParkingApplicationServices(bootstrap, opts, mockMode);

        services.AddSingleton<IUiThreadDispatcher>(_ =>
            new DispatcherQueueUiThreadDispatcher(DispatcherQueue.GetForCurrentThread()));

        services.AddTransient<DashboardViewModel>(sp =>
            new DashboardViewModel(
                sp.GetRequiredService<ILotSnapshotStream>(),
                sp.GetRequiredService<IUiThreadDispatcher>(),
                sp.GetRequiredService<GuiLogger>()));

        services.AddTransient<MapPageViewModel>(sp =>
            new MapPageViewModel(
                sp.GetRequiredService<ILotSnapshotStream>(),
                sp.GetRequiredService<IUiThreadDispatcher>(),
                sp.GetRequiredService<IManualSensorService>(),
                sp.GetRequiredService<IGateOperationsService>()));

        services.AddTransient<HardwarePageViewModel>(sp =>
            new HardwarePageViewModel(
                sp.GetRequiredService<IHardwareStatus>(),
                sp.GetRequiredService<IArduinoMonitoringService>(),
                sp.GetRequiredService<IHardwareConfigurationService>(),
                sp.GetRequiredService<ILogQueryService>(),
                sp.GetRequiredService<IManualSensorService>(),
                sp.GetRequiredService<GuiLogger>(),
                sp.GetRequiredService<IUiThreadDispatcher>()));

        services.AddTransient<HardwareConfigEditorViewModel>(sp =>
            new HardwareConfigEditorViewModel(
                sp.GetRequiredService<SmartParkingLot.Application.Hardware.HardwareConfig>(),
                configPath));

        services.AddTransient<AdminPageViewModel>(sp =>
            new AdminPageViewModel(
                sp.GetRequiredService<IGetSpotRowsQuery>(),
                sp.GetRequiredService<ILotSnapshotStream>()));

        services.AddTransient<LogPageViewModel>(sp =>
            new LogPageViewModel(
                sp.GetRequiredService<ILogQueryService>()));

        services.AddTransient<Pages.DashboardPage>(sp =>
            new Pages.DashboardPage(sp.GetRequiredService<DashboardViewModel>()));
        services.AddTransient<Pages.MapPage>(sp =>
            new Pages.MapPage(sp.GetRequiredService<MapPageViewModel>()));
        services.AddTransient<Pages.LogPage>(sp =>
            new Pages.LogPage(sp.GetRequiredService<LogPageViewModel>()));
        services.AddTransient<Pages.AdminPage>(sp =>
            new Pages.AdminPage(sp.GetRequiredService<AdminPageViewModel>()));
        services.AddTransient<Pages.HardwarePage>(sp =>
            new Pages.HardwarePage(sp.GetRequiredService<HardwarePageViewModel>()));

        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IApplicationStartup>().StartAsync();

        return provider;
    }
}
