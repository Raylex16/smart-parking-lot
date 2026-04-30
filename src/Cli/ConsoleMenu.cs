using SmartParkingLot.Application;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Cli;

public class ConsoleMenu
{
    private readonly ParkingLot _lot;
    private readonly GateController _gateController;
    private readonly ICapacityService _capacityService;
    private readonly IParkingRepository _repository;
    private readonly IEventPublisher _bus;
    private readonly Dictionary<string, Sensor<SpotSensorReading>> _spotSensors;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly IArduinoReader _bridge;
    private readonly SerialCommandDispatcher _dispatcher;

    public ConsoleMenu(
        ParkingLot lot,
        GateController gateController,
        ICapacityService capacityService,
        IParkingRepository repository,
        IEventPublisher bus,
        Dictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        IArduinoReader bridge,
        SerialCommandDispatcher dispatcher)
    {
        _lot = lot;
        _gateController = gateController;
        _capacityService = capacityService;
        _repository = repository;
        _bus = bus;
        _spotSensors = spotSensors;
        _gateSensor = gateSensor;
        _bridge = bridge;
        _dispatcher = dispatcher;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            PrintMenu();
            Console.Write("\nSeleccione una opción: ");
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1": await HandleEntryAsync(); break;
                    case "2": await HandleExitAsync(); break;
                    case "3": await HandleManualSpotReadingAsync(); break;
                    case "4": ShowParkingStatus(); break;
                    case "5": await ShowVehicleHistoryAsync(); break;
                    case "6": await ShowSensorReadingsAsync(); break;
                    case "7": await ShowDeviceActionsAsync(); break;
                    case "8": RunLiveMonitoring(); break;
                    case "9": await ShowSpotsFromDbAsync(); break;
                    case "0":
                        Console.WriteLine("Saliendo...");
                        return;
                    default:
                        Console.WriteLine("Opción inválida.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }

            Console.WriteLine("\nPresione ENTER para continuar...");
            Console.ReadLine();
        }
    }

    private void PrintMenu()
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║          Smart Parking Lot — Menú                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.WriteLine($"  Parqueadero : {_lot.Name} ({_lot.Id})");
        Console.WriteLine($"  Modo        : {_lot.Mode}");
        Console.WriteLine($"  Espacios    : {_lot.TotalSpots} totales | {_lot.AvailableSpots} disponibles");
        Console.WriteLine("  # -> No implementado en esta demo");
        Console.WriteLine("  1. # Solicitar entrada de vehículo");
        Console.WriteLine("  2. # Solicitar salida de vehículo");
        Console.WriteLine("  3. # Actualizar estado de un espacio (sensor manual)");
        Console.WriteLine("  4. Ver estado del parqueadero");
        Console.WriteLine("  5. # Ver historial de un vehículo");
        Console.WriteLine("  6. Ver lecturas de un sensor");
        Console.WriteLine("  7. # Ver acciones de un dispositivo");
        Console.WriteLine("  8. Monitoreo en tiempo real (Arduino)");
        Console.WriteLine("  9. Ver estado de espacios");
        Console.WriteLine("  0. Salir");
    }

    private async Task HandleEntryAsync()
    {
        Console.Write("Placa del vehículo: ");
        var plate = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(plate))
        {
            Console.WriteLine("Placa vacía.");
            return;
        }

        var occupiedBefore = _lot.GetSpots()
            .Where(s => s.IsOccupied)
            .Select(s => s.Id)
            .ToHashSet();

        var gateReading = new GateSensorReading(plate, ENTRY_GATE_ID);
        _gateSensor.CaptureReading(gateReading);
        await _repository.LogSensorReadingAsync(_gateSensor.Id, $"plate:{plate}", DateTime.Now);

        var request = new EntryRequest(plate) { GateId = ENTRY_GATE_ID };
        _gateController.HandleRequest(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "ENTRY", _lot.Id, request.Timestamp, request.Approved);

        if (request.Approved)
        {
            var newlyOccupied = _lot.GetSpots()
                .FirstOrDefault(s => s.IsOccupied && !occupiedBefore.Contains(s.Id));

            if (newlyOccupied is not null)
            {
                await _repository.UpdateSpotStatusAsync(newlyOccupied.Id, true);
            }

            await _repository.LogDeviceActionAsync($"GATE-{ENTRY_GATE_ID}", "OPEN", DateTime.Now);
        }

        Console.WriteLine($"\n[Resultado] {(request.Approved ? "CONCEDIDO ✓" : "DENEGADO ✗")} | Disponibles: {_lot.AvailableSpots}");
    }

    private async Task HandleExitAsync()
    {
        Console.Write("Placa del vehículo: ");
        var plate = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(plate))
        {
            Console.WriteLine("Placa vacía.");
            return;
        }

        var request = new ExitRequest(plate) { GateId = EXIT_GATE_ID };
        _gateController.HandleRequest(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "EXIT", _lot.Id, request.Timestamp, approved: true);
        await _repository.LogDeviceActionAsync($"GATE-{EXIT_GATE_ID}", "OPEN", DateTime.Now);

        Console.WriteLine($"\n[Resultado] Puerta de salida abierta para '{plate}'.");
        Console.WriteLine("[Nota] La liberación del spot la detecta el sensor (use opción 3).");
    }

    private async Task HandleManualSpotReadingAsync()
    {
        Console.WriteLine("Espacios disponibles:");
        foreach (var s in _lot.GetSpots())
            Console.WriteLine($"  {s.Id} -> {(s.IsOccupied ? "OCUPADO" : "LIBRE")}");

        Console.Write("\nID del espacio: ");
        var spotId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(spotId))
        {
            Console.WriteLine("ID vacío.");
            return;
        }

        if (!_spotSensors.TryGetValue(spotId, out var sensor))
        {
            Console.WriteLine($"No hay sensor registrado para el espacio '{spotId}'.");
            return;
        }

        Console.Write("¿Ocupado? (s/n): ");
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        var isOccupied = answer == "s" || answer == "si" || answer == "sí" || answer == "y" || answer == "yes";

        var reading = new SpotSensorReading(spotId, isOccupied);
        sensor.CaptureReading(reading);

        var rawValue = isOccupied ? "1" : "0";
        await _repository.LogSensorReadingAsync(sensor.Id, rawValue, DateTime.Now);

        _bus.Publish(new SensorReadingReceived(
            SensorId: sensor.Id,
            SensorType: sensor.GetSensorType(),
            RawValue: rawValue,
            Timestamp: DateTimeOffset.Now));

        Console.WriteLine($"\n[Resultado] Evento publicado — Espacio '{spotId}' → {(isOccupied ? "OCUPADO" : "LIBRE")}.");
    }

    private void ShowParkingStatus()
    {
        Console.WriteLine($"\nEstado de '{_lot.Name}' ({_lot.Id})");
        Console.WriteLine($"  Disponibles: {_lot.AvailableSpots} / {_lot.TotalSpots}\n");
        foreach (var spot in _lot.GetSpots())
            Console.WriteLine($"  {spot}");
    }

    private async Task ShowVehicleHistoryAsync()
    {
        Console.Write("Placa del vehículo: ");
        var plate = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(plate))
        {
            Console.WriteLine("Placa vacía.");
            return;
        }

        var history = await _repository.GetRequestHistoryAsync(plate);
        var list = history.ToList();

        if (list.Count == 0)
        {
            Console.WriteLine($"No hay historial para '{plate}'.");
            return;
        }

        Console.WriteLine($"\nHistorial de '{plate}' ({list.Count} registro(s)):");
        foreach (var r in list)
        {
            var approved = r.Approved ? "✓ APROBADO" : "✗ DENEGADO";
            Console.WriteLine($"  [{r.Timestamp:yyyy-MM-dd HH:mm:ss}] {r.RequestType,-5} {approved}  ({r.RequestId})");
        }
    }

    private async Task ShowSensorReadingsAsync()
    {
        Console.WriteLine("Sensores conocidos:");
        Console.WriteLine($"  {_gateSensor.Id}  (puerta)");
        foreach (var s in _spotSensors.Values)
            Console.WriteLine($"  {s.Id}  (spot)");

        Console.Write("\nID del sensor: ");
        var sensorId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(sensorId))
        {
            Console.WriteLine("ID vacío.");
            return;
        }

        var readings = (await _repository.GetSensorReadingsAsync(sensorId)).ToList();

        if (readings.Count == 0)
        {
            Console.WriteLine($"No hay lecturas para '{sensorId}'.");
            return;
        }

        Console.WriteLine($"\nLecturas de '{sensorId}' ({readings.Count} registro(s)):");
        foreach (var r in readings)
            Console.WriteLine($"  [{r.Timestamp:yyyy-MM-dd HH:mm:ss}] Valor: {r.Value}  ({r.Id})");
    }

    private async Task ShowDeviceActionsAsync()
    {
        Console.WriteLine("Dispositivos conocidos:");
        Console.WriteLine($"  GATE-{ENTRY_GATE_ID}");
        Console.WriteLine($"  GATE-{EXIT_GATE_ID}");

        Console.Write("\nID del dispositivo: ");
        var deviceId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            Console.WriteLine("ID vacío.");
            return;
        }

        var actions = (await _repository.GetDeviceActionsAsync(deviceId)).ToList();

        if (actions.Count == 0)
        {
            Console.WriteLine($"No hay acciones para '{deviceId}'.");
            return;
        }

        Console.WriteLine($"\nAcciones de '{deviceId}' ({actions.Count} registro(s)):");
        foreach (var a in actions)
            Console.WriteLine($"  [{a.Timestamp:yyyy-MM-dd HH:mm:ss}] {a.Action}  ({a.Id})");
    }

    private async Task ShowSpotsFromDbAsync()
    {
        var spots = (await _repository.GetSpotsByLotIdAsync(_lot.Id)).ToList();

        if (spots.Count == 0)
        {
            Console.WriteLine("No hay espacios registrados en la BD.");
            return;
        }

        Console.WriteLine($"\nEspacios de '{_lot.Name}' en BD ({spots.Count} espacio(s)):");
        Console.WriteLine($"  {"ID",-8} {"Dirección",-28} Estado");
        Console.WriteLine($"  {new string('─', 50)}");

        foreach (var s in spots)
            Console.WriteLine($"  {s.Id,-8} {s.Address,-28} {(s.IsOccupied ? "OCUPADO" : "LIBRE")}");

        var occupied = spots.Count(s => s.IsOccupied);
        Console.WriteLine($"\n  Total: {spots.Count} | Ocupados: {occupied} | Libres: {spots.Count - occupied}");
    }

    private void RunLiveMonitoring()
    {
        Console.WriteLine("\nMonitoreo en tiempo real — los logs de Arduino se mostrarán ahora. Presione cualquier tecla para salir.\n");

        if (!_bridge.IsListening)
        {
            _bridge.StartListening();
        }

        if (!_bridge.IsListening)
        {
            Console.WriteLine("[Monitoreo] Arduino no disponible.");
            return;
        }

        _bridge.ConsoleLoggingEnabled = true;
        _dispatcher.ConsoleLoggingEnabled = true;

        while (!Console.KeyAvailable)
        {
            Thread.Sleep(MONITOR_POLL_DELAY_MS);
        }

        Console.ReadKey(intercept: true);
        _bridge.ConsoleLoggingEnabled = false;
        _dispatcher.ConsoleLoggingEnabled = false;
        Console.WriteLine("\nMonitoreo detenido.");
    }
}
