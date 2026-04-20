using SmartParkingLot.Application;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Cli;

/// <summary>
/// GRASP - Controller: Orquesta la interacción con el usuario y delega a los servicios
/// de aplicación (GateController, CapacityService) y al repositorio de persistencia.
/// GRASP - Low Coupling: Depende de abstracciones (ICapacityService, IParkingRepository,
/// IEventPublisher).
/// </summary>
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

    public ConsoleMenu(
        ParkingLot lot,
        GateController gateController,
        ICapacityService capacityService,
        IParkingRepository repository,
        IEventPublisher bus,
        Dictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        IArduinoReader bridge)
    {
        _lot = lot;
        _gateController = gateController;
        _capacityService = capacityService;
        _repository = repository;
        _bus = bus;
        _spotSensors = spotSensors;
        _gateSensor = gateSensor;
        _bridge = bridge;
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
        Console.WriteLine("  1. Solicitar entrada de vehículo");
        Console.WriteLine("  2. Solicitar salida de vehículo");
        Console.WriteLine("  3. Actualizar estado de un espacio (sensor manual)");
        Console.WriteLine("  4. Ver estado del parqueadero");
        Console.WriteLine("  5. Ver historial de un vehículo");
        Console.WriteLine("  6. Ver lecturas de un sensor");
        Console.WriteLine("  7. Ver acciones de un dispositivo");
        Console.WriteLine("  8. Monitoreo en tiempo real (Arduino)");
        Console.WriteLine("  0. Salir");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Opción 1: Solicitar entrada
    // ═══════════════════════════════════════════════════════════════════
    private async Task HandleEntryAsync()
    {
        Console.Write("Placa del vehículo: ");
        var plate = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(plate))
        {
            Console.WriteLine("Placa vacía.");
            return;
        }

        // Snapshot para detectar qué spot se ocupa
        var occupiedBefore = _lot.GetSpots()
            .Where(s => s.IsOccupied)
            .Select(s => s.Id)
            .ToHashSet();

        // Simulación del sensor de puerta (cámara LPR detecta placa)
        var gateReading = new GateSensorReading(plate, ENTRY_GATE_ID);
        _gateSensor.CaptureReading(gateReading);
        await _repository.LogSensorReadingAsync(_gateSensor.Id, $"plate:{plate}", DateTime.Now);

        // Procesar la solicitud via GateController (GRASP - Controller)
        var request = new EntryRequest(plate) { GateId = ENTRY_GATE_ID };
        _gateController.HandleRequest(request);

        // Persistir la solicitud en RequestLogs
        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "ENTRY", _lot.Id, request.Timestamp, request.Approved);

        if (request.Approved)
        {
            // Persistir el cambio de estado del spot recién ocupado
            var newlyOccupied = _lot.GetSpots()
                .FirstOrDefault(s => s.IsOccupied && !occupiedBefore.Contains(s.Id));

            if (newlyOccupied is not null)
            {
                await _repository.UpdateSpotStatusAsync(newlyOccupied.Id, true);
            }

            // Registrar acción del dispositivo (puerta abierta)
            await _repository.LogDeviceActionAsync($"GATE-{ENTRY_GATE_ID}", "OPEN", DateTime.Now);
        }

        Console.WriteLine($"\n[Resultado] {(request.Approved ? "CONCEDIDO ✓" : "DENEGADO ✗")} | Disponibles: {_lot.AvailableSpots}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Opción 2: Solicitar salida
    // ═══════════════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════════════
    // Opción 3: Actualizar estado de un espacio (sensor manual)
    // Publica SensorReadingReceived en el bus → ejecuta HandleSensorReadingUseCase
    // → dispara Spot.OccupancyChanged → handlers persisten spot_status + LED
    // ═══════════════════════════════════════════════════════════════════
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

        // 1) Captura local del snapshot del sensor (simulación hardware).
        var reading = new SpotSensorReading(spotId, isOccupied);
        sensor.CaptureReading(reading);

        // 2) Persistir la lectura cruda para auditoría (rúbrica: "valor + timestamp").
        var rawValue = isOccupied ? "1" : "0";
        await _repository.LogSensorReadingAsync(sensor.Id, rawValue, DateTime.Now);

        // 3) Publicar el evento en el bus. Esto dispara:
        //    - HandleSensorReadingUseCase (aplica la ocupación al dominio)
        //    - SpotOccupancyChangedHandler (envía comandos al actuador / LED)
        //    - el subscriber de persistencia registrado en Program.cs (UpdateSpotStatusAsync)
        _bus.Publish(new SensorReadingReceived(
            SensorId: sensor.Id,
            SensorType: sensor.GetSensorType(),
            RawValue: rawValue,
            Timestamp: DateTimeOffset.Now));

        Console.WriteLine($"\n[Resultado] Evento publicado — Espacio '{spotId}' → {(isOccupied ? "OCUPADO" : "LIBRE")}.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Opción 4: Estado del parqueadero (en memoria)
    // ═══════════════════════════════════════════════════════════════════
    private void ShowParkingStatus()
    {
        Console.WriteLine($"\nEstado de '{_lot.Name}' ({_lot.Id})");
        Console.WriteLine($"  Disponibles: {_lot.AvailableSpots} / {_lot.TotalSpots}\n");
        foreach (var spot in _lot.GetSpots())
            Console.WriteLine($"  {spot}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Opción 5: Historial de un vehículo (lee desde BD)
    // ═══════════════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════════════
    // Opción 6: Lecturas de un sensor (lee desde BD)
    // ═══════════════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════════════
    // Opción 7: Acciones de un dispositivo (lee desde BD)
    // ═══════════════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════════════
    // Opción 8: Monitoreo en tiempo real (Arduino)
    // ═══════════════════════════════════════════════════════════════════
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

        while (!Console.KeyAvailable)
        {
            Thread.Sleep(MONITOR_POLL_DELAY_MS);
        }

        Console.ReadKey(intercept: true); // consume key
        _bridge.ConsoleLoggingEnabled = false;
        Console.WriteLine("\nMonitoreo detenido.");
    }
}
