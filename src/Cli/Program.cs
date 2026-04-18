using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;
using SmartParkingLot.Application;
using SmartParkingLot.Hardware;


// Program.cs es el único lugar donde se ensamblan las dependencias
// de toda la aplicación.
// Aquí se aplica Dependency Injection manual con top-level statements.


// ── 1. Crear el parqueadero y sus espacios ──

var lot = new ParkingLot("LOT-01", "Campus Barcelona", ParkingMode.AUTOMATIC);
lot.AddSpot(new ParkingSpot("A1", "Zona-A Fila-1", "Estándar",  "Planta Baja"));


// ── 2. Crear sensor ──
var spotSensorA1 = new Sensor<SpotSensorReading>("SEN-SPOT-A1", "Ultrasonido");


// ── 2.1 Bridge serial: conecta Arduino fisico con sensores via ISensorCapture (DIP) ──
// Mapeo: hardware ID -> (spot ID del dominio, sensor)
var sensorMap = new Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)>
{
    ["IR1"] = ("A1", spotSensorA1)
};

using var bridge = new ArduinoSerialBridge(DEFAULT_PORT_NAME, DEFAULT_BAUD_RATE, sensorMap);
bridge.StartListening();


ICapacityService capacityService = new CapacityService(lot);
IAlertService alertService = new AlertService();
var gateController = new GateController(capacityService, alertService);


//funcionamiento con sensor IR
Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║                Smart Parking Lot                 ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine($"Parqueadero : {lot.Name}");
Console.WriteLine($"Modo        : {lot.Mode}");
Console.WriteLine($"Espacios    : {lot.TotalSpots} totales | {lot.AvailableSpots} disponibles");
Console.WriteLine($"Sensores    : {spotSensorA1}");



Console.WriteLine("╔═════════════════════════════════════════════════╗");
Console.WriteLine("║             MONITOREO EN TIEMPO REAL            ║");
Console.WriteLine("╚═════════════════════════════════════════════════╝");

bool? lastState = null;

while (true)
{
    var snapshot = spotSensorA1.GetSnapshot();
    if (snapshot is not null && snapshot.IsOccupied != lastState)
    {
        lastState = snapshot.IsOccupied;
        Console.WriteLine($"\n[Monitoreo] Cambio detectado: {snapshot}");
        capacityService.UpdateSpotState(snapshot);

        foreach (var spot in lot.GetSpots())
            Console.WriteLine($"  {spot}");

        Console.WriteLine($"  Espacios disponibles: {lot.AvailableSpots}");
    }

    Thread.Sleep(MONITOR_POLL_DELAY_MS);
}
