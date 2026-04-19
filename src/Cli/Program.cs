using SmartParkingLot.Application;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

// Composition Root: único lugar donde se ensamblan las dependencias.

// 1. Dominio
var lot = new ParkingLot("LOT-01", "Campus Barcelona", ParkingMode.AUTOMATIC);
lot.AddSpot(new ParkingSpot("A1", "Zona-A Fila-1", "Estándar", "Planta Baja"));

// 2. Bus in-process
IEventPublisher bus = new InProcessEventBus();

// 3. Hardware: bridge (inbound) + dispatcher (outbound)
using var bridge = new ArduinoSerialBridge(DEFAULT_PORT_NAME, DEFAULT_BAUD_RATE, bus);
using var dispatcher = new SerialCommandDispatcher(bridge);

// 4. Caso de uso: lecturas -> dominio
var sensorToSpot = new Dictionary<string, string> { ["IR1"] = "A1" };
var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
bus.Subscribe<SensorReadingReceived>(handleReading.Handle);

// 5. Handler: eventos de dominio -> comandos
var spotToActuator = new Dictionary<string, string> { ["A1"] = "LED1" };
var occupancyHandler = new SpotOccupancyChangedHandler(dispatcher, spotToActuator);

foreach (var spot in lot.GetSpots())
    spot.OccupancyChanged += occupancyHandler.Handle;

// 6. Reglas de capacidad / alertas (compatibilidad con flujo de puertas previo)
ICapacityService capacityService = new CapacityService(lot);
IAlertService alertService = new AlertService();
var gateController = new GateController(capacityService, alertService);
_ = gateController;

// 7. Arrancar
bridge.StartListening();

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║                Smart Parking Lot                 ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine($"Parqueadero : {lot.Name}");
Console.WriteLine($"Modo        : {lot.Mode}");
Console.WriteLine($"Espacios    : {lot.TotalSpots} totales | {lot.AvailableSpots} disponibles");
Console.WriteLine("Flujo bidireccional OK. Ctrl+C para salir.");

var done = new ManualResetEventSlim();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
done.Wait();
