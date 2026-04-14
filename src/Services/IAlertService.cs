using SmartParkingLot.Domain;

namespace SmartParkingLot.Services;

// GRASP - Low Coupling: Interfaz para desacoplar el servicio de alertas de sus consumidores
public interface IAlertService
{
    void GenerateAlert(SensorReading reading);
    IReadOnlyList<Alert> GetAlerts();
    void NotifyAll();
}
