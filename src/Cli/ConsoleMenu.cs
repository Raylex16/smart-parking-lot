using SmartParkingLot.Application;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;
using Spectre.Console;

namespace SmartParkingLot.Cli;

public class ConsoleMenu
{
    private const int RECENT_LOG_LINES = 50;

    private const string OPT_ENTRY    = "Solicitar entrada de vehículo";
    private const string OPT_EXIT_VEH = "Solicitar salida de vehículo";
    private const string OPT_SPOT     = "Actualizar estado de un espacio (sensor manual)";
    private const string OPT_STATUS   = "Ver estado del parqueadero";
    private const string OPT_HISTORY  = "Ver historial de un vehículo";
    private const string OPT_SENSOR   = "Ver lecturas de un sensor";
    private const string OPT_DEVICE   = "Ver acciones de un dispositivo";
    private const string OPT_MONITOR  = "Monitoreo en tiempo real (Arduino)";
    private const string OPT_SPOTS_DB = "Ver estado de espacios (BD)";
    private const string OPT_LOGS     = "Ver logs recientes";
    private const string OPT_SIMULATE = "Simular sensor de puerta (IR)";
    private const string OPT_QUIT     = "Salir";

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
            RenderHeader();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]¿Qué desea hacer?[/]")
                    .PageSize(14)
                    .HighlightStyle(new Style(Color.Green, decoration: Decoration.Bold))
                    .AddChoices(
                        OPT_ENTRY, OPT_EXIT_VEH, OPT_SPOT, OPT_STATUS,
                        OPT_HISTORY, OPT_SENSOR, OPT_DEVICE, OPT_MONITOR,
                        OPT_SPOTS_DB, OPT_LOGS, OPT_SIMULATE, OPT_QUIT));

            if (choice == OPT_QUIT)
            {
                AnsiConsole.MarkupLine("[grey]Saliendo...[/]");
                return;
            }

            AnsiConsole.Clear();

            try
            {
                switch (choice)
                {
                    case OPT_ENTRY:    await HandleEntryAsync();             break;
                    case OPT_EXIT_VEH: await HandleExitAsync();              break;
                    case OPT_SPOT:     await HandleManualSpotReadingAsync(); break;
                    case OPT_STATUS:   ShowParkingStatus();                  break;
                    case OPT_HISTORY:  await ShowVehicleHistoryAsync();      break;
                    case OPT_SENSOR:   await ShowSensorReadingsAsync();      break;
                    case OPT_DEVICE:   await ShowDeviceActionsAsync();       break;
                    case OPT_MONITOR:  RunLiveMonitoring();                  break;
                    case OPT_SPOTS_DB: await ShowSpotsFromDbAsync();         break;
                    case OPT_LOGS:     ShowRecentLogs();                     break;
                    case OPT_SIMULATE: SimulateGateSensor();                 break;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red][[ERROR]][/] {ex.Message.EscapeMarkup()}");
            }

            AnsiConsole.MarkupLine("\n[grey dim]Presione ENTER para continuar...[/]");
            Console.ReadLine();
        }
    }

    private void RenderHeader()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold blue] Smart Parking Lot [/]").RuleStyle("blue dim"));

        var grid = new Grid().AddColumn().AddColumn().AddColumn();
        grid.AddRow(
            $"[grey]Parqueadero:[/] [white]{_lot.Name.EscapeMarkup()}[/] [grey]({_lot.Id.EscapeMarkup()})[/]",
            $"[grey]Modo:[/] [yellow]{_lot.Mode}[/]",
            $"[grey]Disponibles:[/] [green]{_lot.AvailableSpots}[/][grey]/[/][white]{_lot.TotalSpots}[/]");

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    private void HandlePendingApprovals()
    {
        AnsiConsole.Write(new Rule("[bold]Entrada de vehículo[/]").LeftJustified());

        var plate = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]?[/] Placa del vehículo:")
                .Validate(p => !string.IsNullOrWhiteSpace(p)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]La placa no puede estar vacía.[/]")));

        var occupiedBefore = _lot.GetSpots()
            .Where(s => s.IsOccupied).Select(s => s.Id).ToHashSet();

        var gateReading = new GateSensorReading(plate, ENTRY_GATE_ID);
        _gateSensor.CaptureReading(gateReading);
        await _repository.LogSensorReadingAsync(_gateSensor.Id, $"plate:{plate}", DateTime.Now);

        var request = new EntryRequest(plate) { GateId = ENTRY_GATE_ID };
        await _gateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "ENTRY", _lot.Id, request.Timestamp, request.Approved);

        if (request.Approved)
        {
            var newlyOccupied = _lot.GetSpots()
                .FirstOrDefault(s => s.IsOccupied && !occupiedBefore.Contains(s.Id));
            if (newlyOccupied is not null)
                await _repository.UpdateSpotStatusAsync(newlyOccupied.Id, true);
            await _repository.LogDeviceActionAsync($"GATE-{ENTRY_GATE_ID}", "OPEN", DateTime.Now);
        }

        var result = request.Approved ? "[bold green]✓ CONCEDIDO[/]" : "[bold red]✗ DENEGADO[/]";
        AnsiConsole.MarkupLine($"\nResultado: {result} | Disponibles: [yellow]{_lot.AvailableSpots}[/]");
    }

    private async Task HandleChangeModeAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Salida de vehículo[/]").LeftJustified());

        var plate = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]?[/] Placa del vehículo:")
                .Validate(p => !string.IsNullOrWhiteSpace(p)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]La placa no puede estar vacía.[/]")));

        var request = new ExitRequest(plate) { GateId = EXIT_GATE_ID };
        await _gateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _repository.LogRequestAsync(requestId, plate, "EXIT", _lot.Id, request.Timestamp, approved: true);
        await _repository.LogDeviceActionAsync($"GATE-{EXIT_GATE_ID}", "OPEN", DateTime.Now);

        AnsiConsole.MarkupLine($"\n[bold green]✓[/] Puerta de salida abierta para '[yellow]{plate.EscapeMarkup()}[/]'.");
        AnsiConsole.MarkupLine("[grey]La liberación del spot la detecta el sensor.[/]");
    }

    private async Task HandleManualSpotReadingAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Sensor manual de espacio[/]").LeftJustified());

        var spotsTable = new Table().Border(TableBorder.Rounded).Title("[grey]Espacios actuales[/]");
        spotsTable.AddColumn(new TableColumn("ID").Centered());
        spotsTable.AddColumn(new TableColumn("Estado").Centered());
        foreach (var s in _lot.GetSpots())
            spotsTable.AddRow(s.Id.EscapeMarkup(), s.IsOccupied ? "[red]OCUPADO[/]" : "[green]LIBRE[/]");
        AnsiConsole.Write(spotsTable);

        var spotId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]?[/] ID del espacio:")
                .Validate(s => !string.IsNullOrWhiteSpace(s)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]El ID no puede estar vacío.[/]")));

        if (!_spotSensors.TryGetValue(spotId, out var sensor))
        {
            AnsiConsole.MarkupLine($"[red]No hay sensor registrado para el espacio '{spotId.EscapeMarkup()}'.[/]");
            return;
        }

        var isOccupied = AnsiConsole.Confirm("¿Marcar como ocupado?");

        var reading = new SpotSensorReading(spotId, isOccupied);
        sensor.CaptureReading(reading);

        var rawValue = isOccupied ? "1" : "0";
        await _repository.LogSensorReadingAsync(sensor.Id, rawValue, DateTime.Now);

        _bus.Publish(new SensorReadingReceived(
            SensorId: sensor.Id,
            SensorType: sensor.GetSensorType(),
            RawValue: rawValue,
            Timestamp: DateTimeOffset.Now));

        var stateLabel = isOccupied ? "[red]OCUPADO[/]" : "[green]LIBRE[/]";
        AnsiConsole.MarkupLine($"\n[bold green]✓[/] Evento publicado — Espacio '[yellow]{spotId.EscapeMarkup()}[/]' → {stateLabel}.");
    }

    private async Task ShowParkingStatusAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Estado del parqueadero[/]").LeftJustified());

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]{_lot.Name.EscapeMarkup()}[/] [grey]({_lot.Id.EscapeMarkup()})[/]");

        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn(new TableColumn("Estado").Centered());

        foreach (var spot in _lot.GetSpots())
            table.AddRow(spot.Id.EscapeMarkup(), spot.IsOccupied ? "[red]● OCUPADO[/]" : "[green]○ LIBRE[/]");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[grey]Disponibles:[/] [green]{_lot.AvailableSpots}[/] [grey]/[/] [white]{_lot.TotalSpots}[/]");
    }

    private async Task ShowVehicleHistoryAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Historial de vehículo[/]").LeftJustified());

        var plate = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]?[/] Placa del vehículo:")
                .Validate(p => !string.IsNullOrWhiteSpace(p)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]La placa no puede estar vacía.[/]")));

        var history = (await _repository.GetRequestHistoryAsync(plate)).ToList();

        if (history.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No hay historial para '{plate.EscapeMarkup()}'.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"Historial de [bold yellow]{plate.EscapeMarkup()}[/] ({history.Count} registro(s))");

        table.AddColumn("Fecha / Hora");
        table.AddColumn(new TableColumn("Tipo").Centered());
        table.AddColumn(new TableColumn("Estado").Centered());
        table.AddColumn("ID Solicitud");

        foreach (var r in history)
        {
            var approved = r.Approved ? "[bold green]✓ APROBADO[/]" : "[bold red]✗ DENEGADO[/]";
            table.AddRow(
                r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                r.RequestType,
                approved,
                r.RequestId.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    private async Task ShowSensorReadingsAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Lecturas de sensor[/]").LeftJustified());

        var choices = new List<string> { _gateSensor.Id };
        choices.AddRange(_spotSensors.Values.Select(s => s.Id));

        var sensorId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]?[/] Seleccione un sensor:")
                .AddChoices(choices));

        var readings = (await _repository.GetSensorReadingsAsync(sensorId)).ToList();

        if (readings.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No hay lecturas para '{sensorId.EscapeMarkup()}'.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"Lecturas de [bold]{sensorId.EscapeMarkup()}[/] ({readings.Count} registro(s))");

        table.AddColumn("Fecha / Hora");
        table.AddColumn(new TableColumn("Valor").Centered());
        table.AddColumn("ID Lectura");

        foreach (var r in readings)
            table.AddRow(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), r.Value.EscapeMarkup(), r.Id.EscapeMarkup());

        AnsiConsole.Write(table);
    }

    private async Task ShowDeviceActionsAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Acciones de dispositivo[/]").LeftJustified());

        var deviceId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]?[/] Seleccione un dispositivo:")
                .AddChoices($"GATE-{ENTRY_GATE_ID}", $"GATE-{EXIT_GATE_ID}"));

        var actions = (await _repository.GetDeviceActionsAsync(deviceId)).ToList();

        if (actions.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No hay acciones para '{deviceId.EscapeMarkup()}'.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"Acciones de [bold]{deviceId.EscapeMarkup()}[/] ({actions.Count} registro(s))");

        table.AddColumn("Fecha / Hora");
        table.AddColumn(new TableColumn("Acción").Centered());
        table.AddColumn("ID");

        foreach (var a in actions)
            table.AddRow(a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), a.Action.EscapeMarkup(), a.Id.EscapeMarkup());

        AnsiConsole.Write(table);
    }

    private async Task ShowSpotsFromDbAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Espacios en base de datos[/]").LeftJustified());

        var spots = (await _repository.GetSpotsByLotIdAsync(_lot.Id)).ToList();

        if (spots.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No hay espacios registrados en la BD.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]{_lot.Name.EscapeMarkup()}[/] — {spots.Count} espacio(s)");

        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Dirección");
        table.AddColumn(new TableColumn("Estado").Centered());

        foreach (var s in spots)
            table.AddRow(s.Id.EscapeMarkup(), s.Address.EscapeMarkup(), s.IsOccupied ? "[red]● OCUPADO[/]" : "[green]○ LIBRE[/]");

        AnsiConsole.Write(table);

        var occupied = spots.Count(s => s.IsOccupied);
        AnsiConsole.MarkupLine($"\n[grey]Total:[/] {spots.Count} | [red]Ocupados:[/] {occupied} | [green]Libres:[/] {spots.Count - occupied}");
    }

    private void RunLiveMonitoring()
    {
        AnsiConsole.Write(new Rule("[bold]Monitoreo en tiempo real[/]").LeftJustified());
        AnsiConsole.MarkupLine("[grey]Los logs de Arduino se mostrarán ahora. Presione cualquier tecla para salir.[/]\n");

        if (!_bridge.IsListening)
            _bridge.StartListening();

        if (!_bridge.IsListening)
        {
            AnsiConsole.MarkupLine("[yellow]Arduino no disponible.[/]");
            return;
        }

        var previousLevel = _consoleLogger.MinimumLevel;
        _consoleLogger.MinimumLevel = LogLevel.Debug;

        while (!Console.KeyAvailable)
            Thread.Sleep(MONITOR_POLL_DELAY_MS);

        Console.ReadKey(intercept: true);
        _consoleLogger.MinimumLevel = previousLevel;
        AnsiConsole.MarkupLine("\n[grey]Monitoreo detenido.[/]");
    }

    private void SimulateGateSensor()
    {
        AnsiConsole.Write(new Rule("[bold]Simular sensor de puerta (IR)[/]").LeftJustified());

        var gateId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]?[/] Seleccione la puerta:")
                .AddChoices(ENTRY_GATE_ID, EXIT_GATE_ID));

        var irSensorId = gateId switch
        {
            ENTRY_GATE_ID => "GATE-IR1",
            EXIT_GATE_ID  => "GATE-IR2",
            _ => null
        };

        _bus.Publish(new SensorReadingReceived(
            SensorId: irSensorId!,
            SensorType: "IR",
            RawValue: "1",
            Timestamp: DateTimeOffset.Now));

        AnsiConsole.MarkupLine($"\n[bold green]✓[/] Evento publicado: [yellow]{irSensorId!.EscapeMarkup()}[/] → 1");
    }

    // TODO: a futuro permitir seleccionar fecha o rango de fechas para mostrar logs históricos.
    private void ShowRecentLogs()
    {
        AnsiConsole.Write(new Rule("[bold]Logs recientes[/]").LeftJustified());

        var path = _fileLogger.GetCurrentLogFilePath();

        if (!File.Exists(path))
        {
            AnsiConsole.MarkupLine($"[yellow]No hay archivo de log para hoy ({Path.GetFileName(path).EscapeMarkup()}).[/]");
            return;
        }

        var tail = new Queue<string>(RECENT_LOG_LINES);
        foreach (var line in File.ReadLines(path))
        {
            if (tail.Count == RECENT_LOG_LINES)
                tail.Dequeue();
            tail.Enqueue(line);
        }

        AnsiConsole.MarkupLine($"[grey]Últimas {tail.Count} línea(s) de '[/][white]{Path.GetFileName(path).EscapeMarkup()}[/][grey]':[/]\n");
        foreach (var line in tail)
            AnsiConsole.MarkupLine($"  [grey]{line.EscapeMarkup()}[/]");
    }
}
