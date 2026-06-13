using SmartParkingLot.Application.Display;
using SmartParkingLot.Application.Gates;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Application.Bootstrap;

public sealed class ApplicationStartup : IApplicationStartup
{
    private readonly ParkingLot _lot;
    private readonly HardwareConfig _hwConfig;
    private readonly IEventPublisher _bus;
    private readonly IArduinoReader _bridge;
    private readonly ICommandDispatcher _dispatcher;
    private readonly IReadOnlyDictionary<string, Sensor<SpotSensorReading>> _spotSensors;
    private readonly IGateOperationsService _gateOperations;
    private readonly ILicensePlateRecognizer _plateRecognizer;
    private readonly IParkingRepository _repository;
    private readonly ILogger _logger;
    private readonly bool _startBridgeSafe;

    public ApplicationStartup(
        ParkingLot lot,
        HardwareConfig hwConfig,
        IEventPublisher bus,
        IArduinoReader bridge,
        ICommandDispatcher dispatcher,
        IReadOnlyDictionary<string, Sensor<SpotSensorReading>> spotSensors,
        IGateOperationsService gateOperations,
        ILicensePlateRecognizer plateRecognizer,
        IParkingRepository repository,
        ILogger logger,
        bool startBridgeSafe = false)
    {
        _lot             = lot;
        _hwConfig        = hwConfig;
        _bus             = bus;
        _bridge          = bridge;
        _dispatcher      = dispatcher;
        _spotSensors     = spotSensors;
        _gateOperations  = gateOperations;
        _plateRecognizer = plateRecognizer;
        _repository      = repository;
        _logger          = logger;
        _startBridgeSafe = startBridgeSafe;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        var sensorToSpot = new Dictionary<string, string>(_hwConfig.BuildSensorToSpot());
        foreach (var s in _spotSensors.Values)
            sensorToSpot[s.Id] = s.Id.Replace("SEN-SPOT-", "");

        var handleReading = new HandleSensorReadingUseCase(_lot, sensorToSpot);
        _bus.Subscribe<SensorReadingReceived>(handleReading.Handle);

        var spotToActuator   = _hwConfig.BuildSpotToActuator();
        var occupancyHandler = new SpotOccupancyChangedHandler(_dispatcher, spotToActuator);
        var persistHandler   = new PersistSpotOccupancyOnChangeHandler(_repository);

        var lcdDisplay   = new LcdDisplay(_dispatcher);
        var lcdHandler   = new LcdCapacityHandler(_lot, lcdDisplay);

        foreach (var spot in _lot.GetSpots())
        {
            spot.OccupancyChanged += occupancyHandler.Handle;
            spot.OccupancyChanged += lcdHandler.Handle;
        }
        persistHandler.Subscribe(_lot.GetSpots());

        var gateSensorHandler = new GateSensorHandler(
            _gateOperations, _plateRecognizer, lcdDisplay, _logger,
            _hwConfig.BuildGateSensorMapping());
        _bus.SubscribeAsync<SensorReadingReceived>(gateSensorHandler.HandleAsync, _logger, "GateSensorHandler");

        if (_startBridgeSafe)
        {
            try { _bridge.StartListening(); }
            catch (Exception ex)
            {
                _logger.Warn("ApplicationStartup",
                    $"Arduino no disponible al iniciar — el sistema seguirá funcionando offline. ({ex.Message})");
            }
        }
        else
        {
            _bridge.StartListening();
        }

        lcdDisplay.ShowCapacity(_lot.AvailableSpots, _lot.TotalSpots);

        return Task.CompletedTask;
    }
}
