using SmartParkingLot.Application;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Cli;

public class ConsoleMenu
{
    private const int RECENT_LOG_LINES = 50;

    private readonly ParkingLot _lot;
    private readonly IParkingRepository _repository;
    private readonly IEventPublisher _bus;
    private readonly Dictionary<string, Sensor<SpotSensorReading>> _spotSensors;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly IArduinoReader _bridge;
    private readonly IParkingModeService _modeService;
    private readonly IApprovalQueue _approvalQueue;
    private readonly ConsoleLogger _consoleLogger;
    private readonly FileLogger _fileLogger;

    public ConsoleMenu(
        ParkingLot lot,
        IParkingRepository repository,
        IEventPublisher bus,
        Dictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        IArduinoReader bridge,
        IParkingModeService modeService,
        IApprovalQueue approvalQueue,
        ConsoleLogger consoleLogger,
        FileLogger fileLogger)
    {
        _lot = lot;
        _repository = repository;
        _bus = bus;
        _spotSensors = spotSensors;
        _gateSensor = gateSensor;
        _bridge = bridge;
        _modeService = modeService;
        _approvalQueue = approvalQueue;
        _consoleLogger = consoleLogger;
        _fileLogger = fileLogger;
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
                    case "1": SimulateGateSensor(); break;
                    case "2": await HandleManualSpotReadingAsync(); break;
                    case "3": await ShowParkingStatusAsync(); break;
                    case "4": await ShowSensorReadingsAsync(); break;
                    case "5": RunLiveMonitoring(); break;
                    case "6": ShowRecentLogs(); break;
                    case "7": await HandleChangeModeAsync(); break;
                    case "8":
                        if (_lot.Mode != ParkingMode.MANUAL)
                            Console.WriteLine("La opción 8 sólo está disponible en modo MANUAL.");
                        else
                            HandlePendingApprovals();
                        break;
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
        Console.WriteLine();
        Console.WriteLine("  1. Simular sensor de puerta (IR)");
        Console.WriteLine("  2. Actualizar estado de un espacio (sensor manual)");
        Console.WriteLine("  3. Ver estado del parqueadero");
        Console.WriteLine("  4. Ver lecturas de un sensor");
        Console.WriteLine("  5. Monitoreo en tiempo real (Arduino)");
        Console.WriteLine("  6. Ver logs recientes");
        Console.WriteLine("  7. Cambiar modo del parqueadero (AUTOMATIC ↔ MANUAL)");
        if (_lot.Mode == ParkingMode.MANUAL)
            Console.WriteLine("  8. Aprobaciones pendientes");
        Console.WriteLine("  0. Salir");
    }

    private void HandlePendingApprovals()
    {
        var pending = _approvalQueue.GetPending();

        if (pending.Count == 0)
        {
            Console.WriteLine("\nNo hay aprobaciones pendientes.");
            return;
        }

        Console.WriteLine($"\nAprobaciones pendientes ({pending.Count}):");
        Console.WriteLine($"  {"#",-3} {"ID",-13} {"Placa",-16} {"Puerta",-8} Restante");
        Console.WriteLine($"  {new string('─', 55)}");

        var now = DateTime.Now;
        for (var i = 0; i < pending.Count; i++)
        {
            var a = pending[i];
            var remaining = (a.ExpiresAt - now).TotalSeconds;
            var remainingText = remaining > 0 ? $"{remaining:0.0}s" : "EXPIRADA";
            Console.WriteLine($"  {i + 1,-3} {a.Id,-13} {a.VehiclePlate,-16} {a.GateId,-8} {remainingText}");
        }

        Console.Write("\n# a procesar (ENTER cancela): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("Cancelado.");
            return;
        }

        if (!int.TryParse(input, out var index) || index < 1 || index > pending.Count)
        {
            Console.WriteLine($"# inválido. Use un valor entre 1 y {pending.Count}.");
            return;
        }

        var approval = pending[index - 1];
        if (approval.IsResolved)
        {
            Console.WriteLine($"Aprobación '{approval.Id}' ya fue resuelta.");
            return;
        }

        Console.Write("A = aprobar, D = denegar: ");
        var decision = Console.ReadLine()?.Trim().ToUpperInvariant();
        switch (decision)
        {
            case "A":
                approval.Approve();
                Console.WriteLine($"{approval.Id} → APROBADA.");
                break;
            case "D":
                approval.Deny();
                Console.WriteLine($"{approval.Id} → DENEGADA.");
                break;
            default:
                Console.WriteLine($"Opción '{decision}' no reconocida. Aprobación intacta.");
                break;
        }
    }

    private async Task HandleChangeModeAsync()
    {
        Console.WriteLine($"\nModo actual: {_modeService.Current}");
        Console.WriteLine("  A. AUTOMATIC");
        Console.WriteLine("  M. MANUAL");
        Console.Write("\nNuevo modo (A/M, ENTER cancela): ");

        var input = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("Cancelado.");
            return;
        }

        var target = input switch
        {
            "A" => ParkingMode.AUTOMATIC,
            "M" => ParkingMode.MANUAL,
            _   => (ParkingMode?)null
        };

        if (target is null)
        {
            Console.WriteLine($"Opción '{input}' no reconocida.");
            return;
        }

        await _modeService.SwitchToAsync(target.Value);
        Console.WriteLine($"\nModo actualizado a {_modeService.Current}.");
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

    private async Task ShowParkingStatusAsync()
    {
        var spots = (await _repository.GetSpotsByLotIdAsync(_lot.Id)).ToList();

        Console.WriteLine($"\nEstado de '{_lot.Name}' ({_lot.Id})");
        Console.WriteLine($"  Disponibles (memoria) : {_lot.AvailableSpots} / {_lot.TotalSpots}\n");

        if (spots.Count == 0)
        {
            Console.WriteLine("No hay espacios registrados en la BD.");
            return;
        }

        Console.WriteLine($"  {"ID",-8} {"Dirección",-28} Estado");
        Console.WriteLine($"  {new string('─', 50)}");

        foreach (var s in spots)
            Console.WriteLine($"  {s.Id,-8} {s.Address,-28} {(s.IsOccupied ? "OCUPADO" : "LIBRE")}");

        var occupied = spots.Count(s => s.IsOccupied);
        Console.WriteLine($"\n  Total: {spots.Count} | Ocupados: {occupied} | Libres: {spots.Count - occupied}");
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

        var previousLevel = _consoleLogger.MinimumLevel;
        _consoleLogger.MinimumLevel = LogLevel.Debug;

        while (!Console.KeyAvailable)
        {
            Thread.Sleep(MONITOR_POLL_DELAY_MS);
        }

        Console.ReadKey(intercept: true);
        _consoleLogger.MinimumLevel = previousLevel;
        Console.WriteLine("\nMonitoreo detenido.");
    }

    private void SimulateGateSensor()
    {
        Console.WriteLine($"Puertas configuradas: {ENTRY_GATE_ID} (entrada) | {EXIT_GATE_ID} (salida)");
        Console.Write($"\nID de la puerta: ");
        var gateId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(gateId))
        {
            Console.WriteLine("ID vacío.");
            return;
        }

        var irSensorId = gateId switch
        {
            ENTRY_GATE_ID => "GATE-IR1",
            EXIT_GATE_ID  => "GATE-IR2",
            _ => null
        };

        if (irSensorId is null)
        {
            Console.WriteLine($"Puerta '{gateId}' no reconocida. Use {ENTRY_GATE_ID} o {EXIT_GATE_ID}.");
            return;
        }

        _bus.Publish(new SensorReadingReceived(
            SensorId: irSensorId,
            SensorType: "IR",
            RawValue: "1",
            Timestamp: DateTimeOffset.Now));

        Console.WriteLine($"\n[Simulado] Evento publicado: {irSensorId} → 1");
    }

    // TODO: a futuro permitir seleccionar fecha o rango de fechas para mostrar logs históricos.
    private void ShowRecentLogs()
    {
        var path = _fileLogger.GetCurrentLogFilePath();

        if (!File.Exists(path))
        {
            Console.WriteLine($"\nNo hay archivo de log para hoy ({path}).");
            return;
        }

        var tail = new Queue<string>(RECENT_LOG_LINES);
        foreach (var line in File.ReadLines(path))
        {
            if (tail.Count == RECENT_LOG_LINES)
                tail.Dequeue();
            tail.Enqueue(line);
        }

        Console.WriteLine($"\nÚltimas {tail.Count} línea(s) de '{Path.GetFileName(path)}':\n");
        foreach (var line in tail)
            Console.WriteLine($"  {line}");
    }
}
