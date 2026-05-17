using SmartParkingLot.Application.Hardware;

namespace SmartParkingLot.Hardware;

/// Adapter that wraps ArduinoSerialBridge and exposes IHardwareStatus.
/// ArduinoSerialBridge does not raise a connection-change event, so we poll
/// IsListening every second with a lightweight Timer.
public sealed class ArduinoHardwareStatus : IHardwareStatus, IDisposable
{
    private readonly ArduinoSerialBridge _bridge;
    private readonly string _portName;
    private readonly Timer _pollTimer;
    private bool _lastConnected;

    public event EventHandler? Changed;

    public bool IsConnected => _bridge.IsListening;

    public string DisplayName => _bridge.IsListening
        ? $"Arduino {_portName}"
        : "Arduino — desconectado";

    public ArduinoHardwareStatus(ArduinoSerialBridge bridge, string portName)
    {
        _bridge = bridge;
        _portName = portName;
        _lastConnected = bridge.IsListening;

        // Poll every second to detect connection-state changes.
        _pollTimer = new Timer(Poll, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void Poll(object? _)
    {
        var current = _bridge.IsListening;
        if (current != _lastConnected)
        {
            _lastConnected = current;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _pollTimer.Dispose();
}
