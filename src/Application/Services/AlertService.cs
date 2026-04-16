using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Application;

// GRASP - Pure Fabrication: Servicio que no representa un concepto del dominio real,
// sino una fabricación para manejar la responsabilidad de alertas sin sobrecargar al controlador
public class AlertService : IAlertService
{
    private readonly List<Alert> _alerts = [];
    private int _alertCounter;

    public void GenerateAlert(SensorReading reading)
    {
        _alertCounter++;
        var alertId = $"ALR-{_alertCounter:D3}";

        var (type, message) = reading switch
        {
            GateSensorReading gsr => ("GATE", $"Lectura de puerta {gsr.GateId} — Placa: {gsr.Plate}"),
            SpotSensorReading ssr => ("SPOT", $"Espacio {ssr.SpotId} — Ocupado: {(ssr.IsOccupied ? "Sí" : "No")}"),
            _ => ("GENERAL", $"Lectura genérica — Valor: {reading.RegisteredValue}")
        };

        var alert = new Alert(alertId, type, message);
        _alerts.Add(alert);
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
