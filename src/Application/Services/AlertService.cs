using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

// GRASP - Pure Fabrication: Servicio que no representa un concepto del dominio real,
// sino una fabricación para manejar la responsabilidad de alertas sin sobrecargar al controlador
public class AlertService : IAlertService
{
    private readonly List<Alert> _alerts = [];
    private readonly IParkingRepository? _repository;
    private int _alertCounter;

    public AlertService(IParkingRepository? repository = null)
    {
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
        
        // Persistir en BD si el repositorio está disponible (fire-and-forget)
        if (_repository != null)
        {
            _ = _repository.LogAlertAsync(alertId, type, message, alert.Date);
        }
        
        alert.Notify();
    }

    public IReadOnlyList<Alert> GetAlerts() => _alerts.AsReadOnly();

    public void NotifyAll()
    {
        Console.WriteLine($"[AlertService] Notificando {_alerts.Count} alerta(s) pendientes:");
        foreach (var alert in _alerts)
        {
            alert.Notify();
        }
    }
}
