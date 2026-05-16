using System.Collections.Generic;
using SmartParkingLot.Application;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Gui.Bootstrap;

/// <summary>
/// Thin adapter that exposes the same surface as the former ParkingServices
/// god-object, but sourced from individual DI-registered singletons.
/// Allows existing page code to compile without change while ParkingServices
/// is removed as an injection point.
///
/// Pages should be progressively refactored to depend on individual services;
/// once all pages are refactored this facade can be deleted.
/// </summary>
public sealed class ParkingServicesFacade
{
    public ParkingLot Lot                          { get; }
    public GateController GateController           { get; }
    public ICapacityService CapacityService        { get; }
    public IAlertService AlertService              { get; }
    public IParkingRepository Repository           { get; }
    public IEventPublisher Bus                     { get; }
    public IReadOnlyDictionary<string, Sensor<SpotSensorReading>> SpotSensors { get; }
    public Sensor<GateSensorReading> GateSensor    { get; }
    public ArduinoSerialBridge Bridge              { get; }
    public IHardwareStatus HardwareStatus          { get; }
    public SerialCommandDispatcher Dispatcher      { get; }
    public GuiLogger UiLogger                      { get; }
    public FileLogger FileLogger                   { get; }
    public HardwareConfig Config                   { get; }

    public ParkingServicesFacade(
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
        Lot              = lot;
        GateController   = gateController;
        CapacityService  = capacityService;
        AlertService     = alertService;
        Repository       = repository;
        Bus              = bus;
        SpotSensors      = spotSensors;
        GateSensor       = gateSensor;
        Bridge           = bridge;
        HardwareStatus   = hardwareStatus;
        Dispatcher       = dispatcher;
        UiLogger         = uiLogger;
        FileLogger       = fileLogger;
        Config           = config;
    }
}
