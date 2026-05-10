using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

public class AlertService : IAlertService
{
    private const string LogSource = "AlertService";

    private readonly List<Alert> _alerts = [];
    private readonly IParkingRepository? _repository;
    private readonly ILogger _logger;
    private int _alertCounter;

    public AlertService(ILogger logger, IParkingRepository? repository = null)
    {
        _logger = logger;
        _repository = repository;
    }

    public void GenerateAlert(SensorReading reading)
    {
        _alertCounter++;
        var alertId = $"{ALERT_ID_PREFIX}{_alertCounter:D3}";

        var (type, message) = reading switch
        {
            GateSensorReading gsr => ("GATE", $"Lectura de puerta {gsr.GateId} — Placa: {gsr.Plate}"),
            SpotSensorReading ssr => ("SPOT", $"Espacio {ssr.SpotId} — Ocupado: {(ssr.IsOccupied ? "Sí" : "No")}"),
            _ => ("GENERAL", $"Lectura genérica — Valor: {reading.RegisteredValue}")
        };

        var alert = new Alert(alertId, type, message);
        _alerts.Add(alert);

        if (_repository != null)
        {
            _ = _repository.LogAlertAsync(alertId, type, message, alert.Date);
        }

        _logger.Warn(LogSource, $"{alert} (Fecha: {alert.Date:yyyy-MM-dd HH:mm:ss})");
    }

    public IReadOnlyList<Alert> GetAlerts() => _alerts.AsReadOnly();

    public void NotifyAll()
    {
        _logger.Info(LogSource, $"Notificando {_alerts.Count} alerta(s) pendientes");
        foreach (var alert in _alerts)
        {
            _logger.Warn(LogSource, alert.ToString());
        }
    }
}
