using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Handlers;

public sealed class GateSensorHandler
{
    private const string LogSource = "GateSensorHandler";

    private readonly IGateRequestHandler _handler;
    private readonly ILicensePlateRecognizer _plateRecognizer;
    private readonly IDisplay _display;
    private readonly ILogger _logger;
    private readonly IReadOnlyDictionary<string, (string GateId, GateType Type)> _gateSensorMapping;

    public GateSensorHandler(
        IGateRequestHandler handler,
        ILicensePlateRecognizer plateRecognizer,
        IDisplay display,
        ILogger logger,
        IReadOnlyDictionary<string, (string GateId, GateType Type)> gateSensorMapping)
    {
        _handler = handler;
        _plateRecognizer = plateRecognizer;
        _display = display;
        _logger = logger;
        _gateSensorMapping = gateSensorMapping;
    }

    public async Task HandleAsync(SensorReadingReceived evt)
    {
        if (!_gateSensorMapping.TryGetValue(evt.SensorId, out var gate)) return;
        if (evt.RawValue != "1") return;

        var plate = await _plateRecognizer.RecognizeAsync(gate.GateId).ConfigureAwait(false);
        _logger.Info(LogSource, $"Vehículo detectado en {gate.GateId} ({gate.Type}) → {plate}");


        switch (gate.Type)
        {
            case GateType.ENTRY:
                var entry = new EntryRequest(plate) { GateId = gate.GateId };
                await _handler.HandleRequestAsync(entry).ConfigureAwait(false);
                _display.ShowMessage(entry.Approved ? "BIENVENIDO" : "LLENO");
                break;

            case GateType.EXIT:
                var exit = new ExitRequest(plate) { GateId = gate.GateId };
                await _handler.HandleRequestAsync(exit).ConfigureAwait(false);
                _display.ShowMessage("GRACIAS");
                break;

            default:
                throw new InvalidOperationException($"Tipo de puerta desconocido: {gate.Type}");
        }
    }
}
