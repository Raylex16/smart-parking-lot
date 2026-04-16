namespace SmartParkingLot.Core.Ports;

// GRASP - Low Coupling: Interfaz para desacoplar el servicio de alertas de sus consumidores
// SOLID - Dependency Inversion Principle: Los módulos de alto nivel dependen de esta
// abstracción, no de la implementación concreta (AlertService).
public interface IAlertService
{
    void GenerateAlert(SensorReading reading);
    IReadOnlyList<Alert> GetAlerts();
    void NotifyAll();
}
