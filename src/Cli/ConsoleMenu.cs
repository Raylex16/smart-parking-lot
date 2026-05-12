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

    private const string OPT_SIMULATE  = "Simular sensor de puerta (IR)";
    private const string OPT_SPOT      = "Actualizar estado de un espacio (sensor manual)";
    private const string OPT_STATUS    = "Ver estado del parqueadero";
    private const string OPT_SENSOR    = "Ver lecturas de un sensor";
    private const string OPT_MONITOR   = "Monitoreo en tiempo real (Arduino)";
    private const string OPT_LOGS      = "Ver logs recientes";
    private const string OPT_MODE      = "Cambiar modo del parqueadero (AUTOMATIC ↔ MANUAL)";
    private const string OPT_APPROVALS = "Aprobaciones pendientes";
    private const string OPT_QUIT      = "Salir";

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

            var choices = new List<string>
            {
                OPT_SIMULATE, OPT_SPOT, OPT_STATUS, OPT_SENSOR,
                OPT_MONITOR, OPT_LOGS, OPT_MODE
            };
            if (_lot.Mode == ParkingMode.MANUAL)
                choices.Add(OPT_APPROVALS);
            choices.Add(OPT_QUIT);

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]¿Qué desea hacer?[/]")
                    .PageSize(14)
                    .HighlightStyle(new Style(Color.Green, decoration: Decoration.Bold))
                    .AddChoices(choices));

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
                    case OPT_SIMULATE:  SimulateGateSensor();                 break;
                    case OPT_SPOT:      await HandleManualSpotReadingAsync(); break;
                    case OPT_STATUS:    await ShowParkingStatusAsync();       break;
                    case OPT_SENSOR:    await ShowSensorReadingsAsync();      break;
                    case OPT_MONITOR:   RunLiveMonitoring();                  break;
                    case OPT_LOGS:      ShowRecentLogs();                     break;
                    case OPT_MODE:      await HandleChangeModeAsync();        break;
                    case OPT_APPROVALS: HandlePendingApprovals();             break;
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

    private async Task HandleChangeModeAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Cambiar modo del parqueadero[/]").LeftJustified());
        AnsiConsole.MarkupLine($"\nModo actual: [yellow]{_modeService.Current}[/]\n");

        var target = AnsiConsole.Prompt(
            new SelectionPrompt<ParkingMode>()
                .Title("Nuevo modo:")
                .AddChoices(ParkingMode.AUTOMATIC, ParkingMode.MANUAL)
                .UseConverter(m => m.ToString()));

        if (target == _modeService.Current)
        {
            AnsiConsole.MarkupLine($"[grey]El modo ya es {target}. Sin cambios.[/]");
            return;
        }

        await _modeService.SwitchToAsync(target);
        AnsiConsole.MarkupLine($"\n[bold green]✓[/] Modo actualizado a [yellow]{_modeService.Current}[/].");
    }

    private void HandlePendingApprovals()
    {
        AnsiConsole.Write(new Rule("[bold]Aprobaciones pendientes[/]").LeftJustified());

        var pending = _approvalQueue.GetPending();

        if (pending.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No hay aprobaciones pendientes.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("#").Centered());
        table.AddColumn("ID");
        table.AddColumn("Placa");
        table.AddColumn(new TableColumn("Puerta").Centered());
        table.AddColumn(new TableColumn("Restante").Centered());

        var now = DateTime.Now;
        for (var i = 0; i < pending.Count; i++)
        {
            var a = pending[i];
            var remaining = (a.ExpiresAt - now).TotalSeconds;
            var remainingText = remaining > 0
                ? $"[green]{remaining:0.0}s[/]"
                : "[red]EXPIRADA[/]";

            table.AddRow(
                (i + 1).ToString(),
                a.Id.EscapeMarkup(),
                a.VehiclePlate.EscapeMarkup(),
                a.GateId.EscapeMarkup(),
                remainingText);
        }

        AnsiConsole.Write(table);

        var index = AnsiConsole.Prompt(
            new TextPrompt<int>($"[green]?[/] # a procesar (0 para cancelar):")
                .DefaultValue(0)
                .Validate(i => i >= 0 && i <= pending.Count
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[red]Use un valor entre 0 y {pending.Count}.[/]")));

        if (index == 0)
        {
            AnsiConsole.MarkupLine("[grey]Cancelado.[/]");
            return;
        }

        var approval = pending[index - 1];
        if (approval.IsResolved)
        {
            AnsiConsole.MarkupLine($"[yellow]Aprobación '{approval.Id.EscapeMarkup()}' ya fue resuelta.[/]");
            return;
        }

        var decision = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Decisión para [yellow]{approval.Id.EscapeMarkup()}[/]:")
                .AddChoices("Aprobar", "Denegar"));

        if (decision == "Aprobar")
        {
            approval.Approve();
            AnsiConsole.MarkupLine($"\n[bold green]✓[/] {approval.Id.EscapeMarkup()} → [green]APROBADA[/].");
        }
        else
        {
            approval.Deny();
            AnsiConsole.MarkupLine($"\n[bold red]✗[/] {approval.Id.EscapeMarkup()} → [red]DENEGADA[/].");
        }
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

        var spots = (await _repository.GetSpotsByLotIdAsync(_lot.Id)).ToList();

        if (spots.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No hay espacios registrados en la BD.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]{_lot.Name.EscapeMarkup()}[/] [grey]({_lot.Id.EscapeMarkup()})[/]");

        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Dirección");
        table.AddColumn(new TableColumn("Estado").Centered());

        foreach (var s in spots)
            table.AddRow(s.Id.EscapeMarkup(), s.Address.EscapeMarkup(),
                s.IsOccupied ? "[red]● OCUPADO[/]" : "[green]○ LIBRE[/]");

        AnsiConsole.Write(table);

        var occupied = spots.Count(s => s.IsOccupied);
        AnsiConsole.MarkupLine($"\n[grey]Total:[/] {spots.Count} | [red]Ocupados:[/] {occupied} | [green]Libres:[/] {spots.Count - occupied}");
        AnsiConsole.MarkupLine($"[grey]Memoria:[/] [green]{_lot.AvailableSpots}[/] [grey]disponibles de[/] [white]{_lot.TotalSpots}[/]");
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
