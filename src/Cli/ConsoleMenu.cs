using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Application.Gates;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Application.Monitoring;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Sensors;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
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

    private readonly ILotSnapshotStream _stream;
    private readonly IGetSpotRowsQuery _spotRowsQuery;
    private readonly IGetSensorReadingsQuery _sensorReadingsQuery;
    private readonly IManualSensorService _manualSensor;
    private readonly IGateOperationsService _gateOperations;
    private readonly IApprovalDecisionService _approvalDecisions;
    private readonly IArduinoMonitoringService _monitoring;
    private readonly IParkingModeService _modeService;
    private readonly ILogQueryService _logQuery;
    private readonly ConsoleLogger _consoleLogger;

    public ConsoleMenu(
        ILotSnapshotStream stream,
        IGetSpotRowsQuery spotRowsQuery,
        IGetSensorReadingsQuery sensorReadingsQuery,
        IManualSensorService manualSensor,
        IGateOperationsService gateOperations,
        IApprovalDecisionService approvalDecisions,
        IArduinoMonitoringService monitoring,
        IParkingModeService modeService,
        ILogQueryService logQuery,
        ConsoleLogger consoleLogger)
    {
        _stream            = stream;
        _spotRowsQuery     = spotRowsQuery;
        _sensorReadingsQuery = sensorReadingsQuery;
        _manualSensor      = manualSensor;
        _gateOperations    = gateOperations;
        _approvalDecisions = approvalDecisions;
        _monitoring        = monitoring;
        _modeService       = modeService;
        _logQuery          = logQuery;
        _consoleLogger     = consoleLogger;
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
            if (_modeService.Current == ParkingMode.MANUAL)
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
                    case OPT_SIMULATE:  await SimulateGateSensorAsync();       break;
                    case OPT_SPOT:      await HandleManualSpotReadingAsync();   break;
                    case OPT_STATUS:    await ShowParkingStatusAsync();         break;
                    case OPT_SENSOR:    await ShowSensorReadingsAsync();        break;
                    case OPT_MONITOR:   RunLiveMonitoring();                    break;
                    case OPT_LOGS:      ShowRecentLogs();                       break;
                    case OPT_MODE:      await HandleChangeModeAsync();          break;
                    case OPT_APPROVALS: HandlePendingApprovals();               break;
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

        var snap = _stream.Current;
        var available = snap.TotalSpots - snap.OccupiedSpots;
        var grid = new Grid().AddColumn().AddColumn().AddColumn();
        grid.AddRow(
            $"[grey]Parqueadero:[/] [white]{snap.Name.EscapeMarkup()}[/] [grey]({snap.Id})[/]",
            $"[grey]Modo:[/] [yellow]{_modeService.Current}[/]",
            $"[grey]Disponibles:[/] [green]{available}[/][grey]/[/][white]{snap.TotalSpots}[/]");

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

        var pending = _approvalDecisions.GetPending();

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

        var approved = decision == "Aprobar";
        _approvalDecisions.Resolve(approval.Id, approved);

        if (approved)
            AnsiConsole.MarkupLine($"\n[bold green]✓[/] {approval.Id.EscapeMarkup()} → [green]APROBADA[/].");
        else
            AnsiConsole.MarkupLine($"\n[bold red]✗[/] {approval.Id.EscapeMarkup()} → [red]DENEGADA[/].");
    }

    private async Task HandleManualSpotReadingAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Sensor manual de espacio[/]").LeftJustified());

        var spots = _stream.Current.Spots;
        var spotsTable = new Table().Border(TableBorder.Rounded).Title("[grey]Espacios actuales[/]");
        spotsTable.AddColumn(new TableColumn("ID").Centered());
        spotsTable.AddColumn(new TableColumn("Estado").Centered());
        foreach (var s in spots)
            spotsTable.AddRow(s.Id.EscapeMarkup(), s.IsOccupied ? "[red]OCUPADO[/]" : "[green]LIBRE[/]");
        AnsiConsole.Write(spotsTable);

        var spotId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]?[/] ID del espacio:")
                .Validate(s => !string.IsNullOrWhiteSpace(s)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]El ID no puede estar vacío.[/]")));

        var isOccupied = AnsiConsole.Confirm("¿Marcar como ocupado?");

        await _manualSensor.RecordSpotReadingAsync(spotId, isOccupied);

        var stateLabel = isOccupied ? "[red]OCUPADO[/]" : "[green]LIBRE[/]";
        AnsiConsole.MarkupLine($"\n[bold green]✓[/] Evento publicado — Espacio '[yellow]{spotId.EscapeMarkup()}[/]' → {stateLabel}.");
    }

    private async Task ShowParkingStatusAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Estado del parqueadero[/]").LeftJustified());

        var snap  = _stream.Current;
        var lotId = snap.Id;
        var spots = (await _spotRowsQuery.ExecuteAsync(lotId)).ToList();

        if (spots.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No hay espacios registrados en la BD.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]{snap.Name.EscapeMarkup()}[/] [grey]({snap.Id})[/]");

        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Dirección");
        table.AddColumn(new TableColumn("Estado").Centered());

        foreach (var s in spots)
            table.AddRow(s.Id.EscapeMarkup(), s.Address.EscapeMarkup(),
                s.IsOccupied ? "[red]● OCUPADO[/]" : "[green]○ LIBRE[/]");

        AnsiConsole.Write(table);

        var occupied = spots.Count(s => s.IsOccupied);
        AnsiConsole.MarkupLine($"\n[grey]Total:[/] {spots.Count} | [red]Ocupados:[/] {occupied} | [green]Libres:[/] {spots.Count - occupied}");
        AnsiConsole.MarkupLine($"[grey]Memoria:[/] [green]{snap.TotalSpots - snap.OccupiedSpots}[/] [grey]disponibles de[/] [white]{snap.TotalSpots}[/]");
    }

    private async Task ShowSensorReadingsAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Lecturas de sensor[/]").LeftJustified());

        var sensorId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]?[/] Seleccione un sensor:")
                .AddChoices(_manualSensor.SensorIds));

        var readings = (await _sensorReadingsQuery.ExecuteAsync(sensorId)).ToList();

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

        if (!_monitoring.IsRunning)
            _monitoring.Start();

        if (!_monitoring.IsRunning)
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

    private async Task SimulateGateSensorAsync()
    {
        AnsiConsole.Write(new Rule("[bold]Simular sensor de puerta (IR)[/]").LeftJustified());

        var gates = _gateOperations.GetRegisteredGates();
        var gateChoices = gates.Select(g => g.GateId).ToList();

        var gateId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]?[/] Seleccione la puerta:")
                .AddChoices(gateChoices));

        await _manualSensor.TriggerGateIrAsync(gateId);
        AnsiConsole.MarkupLine($"\n[bold green]✓[/] Evento IR publicado para puerta [yellow]{gateId.EscapeMarkup()}[/].");
    }

    private void ShowRecentLogs()
    {
        AnsiConsole.Write(new Rule("[bold]Logs recientes[/]").LeftJustified());

        var path = _logQuery.GetCurrentLogFilePath();
        var tail = _logQuery.TailLogFile(RECENT_LOG_LINES);

        if (tail.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No hay archivo de log para hoy ({Path.GetFileName(path).EscapeMarkup()}).[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[grey]Últimas {tail.Count} línea(s) de '[/][white]{Path.GetFileName(path).EscapeMarkup()}[/][grey]':[/]\n");
        foreach (var line in tail)
            AnsiConsole.MarkupLine($"  [grey]{line.EscapeMarkup()}[/]");
    }
}
