namespace SmartParkingLot.Application.Monitoring;

public interface IArduinoMonitoringService
{
    bool IsRunning { get; }
    void Start();
    void Stop();
}
