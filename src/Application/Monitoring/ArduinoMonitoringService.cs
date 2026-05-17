using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Monitoring;

public sealed class ArduinoMonitoringService : IArduinoMonitoringService
{
    private readonly IArduinoReader _bridge;

    public ArduinoMonitoringService(IArduinoReader bridge)
    {
        _bridge = bridge;
    }

    public bool IsRunning => _bridge.IsListening;

    public void Start() => _bridge.StartListening();

    public void Stop() => _bridge.StopListening();
}
