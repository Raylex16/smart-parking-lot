using SmartParkingLot.Application.Gates;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Handlers;

public sealed class GateSensorHandler
{
    private const string LogSource = "GateSensorHandler";

    private readonly IGateOperationsService _gateOps;
    private readonly ILicensePlateRecognizer _plateRecognizer;
    private readonly IDisplay _display;
    private readonly ILogger _logger;
    private readonly IReadOnlyDictionary<string, (string GateId, GateType Type)> _gateSensorMapping;

    public GateSensorHandler(
        IGateOperationsService gateOps,
        ILicensePlateRecognizer plateRecognizer,
        IDisplay display,
        ILogger logger,
        IReadOnlyDictionary<string, (string GateId, GateType Type)> gateSensorMapping)
    {
        _gateOps = gateOps;
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

        // Se delega en GateOperationsService para unificar la persistencia
        // (RequestLogs con el LotId correcto) con la ruta del botón de la GUI.
        switch (gate.Type)
        {
            case GateType.ENTRY:
                var result = await _gateOps.RequestEntryAsync(plate, gate.GateId).ConfigureAwait(false);
                _display.ShowMessage(result.Approved ? "BIENVENIDO" : "LLENO");
                break;

            case GateType.EXIT:
                await _gateOps.RequestExitAsync(plate, gate.GateId).ConfigureAwait(false);
                _display.ShowMessage("GRACIAS");
                break;

            default:
                throw new InvalidOperationException($"Tipo de puerta desconocido: {gate.Type}");
        }
    }
}
