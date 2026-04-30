namespace SmartParkingLot.Core.Interfaces;

public interface IAlertService
{
    void GenerateAlert(SensorReading reading);
    IReadOnlyList<Alert> GetAlerts();
    void NotifyAll();
}
