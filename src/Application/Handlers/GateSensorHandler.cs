using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Handlers;

public sealed class GateSensorHandler
{
    private const string LogSource = "GateSensorHandler";

    private readonly IRequestDispatcher _dispatcher;
    private readonly ILicensePlateRecognizer _plateRecognizer;
    private readonly IDisplay _display;
    private readonly ILogger _logger;
    private readonly IReadOnlyDictionary<string, (string GateId, GateType Type)> _gateSensorMapping;

    public GateSensorHandler(
        IRequestDispatcher dispatcher,
        ILicensePlateRecognizer plateRecognizer,
        IDisplay display,
        ILogger logger,
        IReadOnlyDictionary<string, (string GateId, GateType Type)> gateSensorMapping)
    {
        _dispatcher = dispatcher;
        _plateRecognizer = plateRecognizer;
        _display = display;
        _logger = logger;
        _gateSensorMapping = gateSensorMapping;
    }

    public void Handle(SensorReadingReceived evt)
    {
        if (!_gateSensorMapping.TryGetValue(evt.SensorId, out var gate)) return;
        if (evt.RawValue != "1") return;

        var plate = _plateRecognizer.Recognize(gate.GateId);
        _logger.Info(LogSource, $"Vehículo detectado en {gate.GateId} ({gate.Type}) → {plate}");

        switch (gate.Type)
        {
            case GateType.ENTRY:
                var entry = new EntryRequest(plate) { GateId = gate.GateId };
                _dispatcher.HandleRequest(entry);
                _display.ShowMessage(entry.Approved ? "BIENVENIDO" : "LLENO");
                break;

            case GateType.EXIT:
                var exit = new ExitRequest(plate) { GateId = gate.GateId };
                _dispatcher.HandleRequest(exit);
                _display.ShowMessage("GRACIAS");
                break;

            default:
                throw new InvalidOperationException($"Tipo de puerta desconocido: {gate.Type}");
        }
    }
}
