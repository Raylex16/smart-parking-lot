using SmartParkingLot.Application.Hardware;

namespace SmartParkingLot.Hardware;

/// <summary>
/// IHardwareStatus implementation for mock/simulation mode.
/// Always reports <c>IsConnected = true</c> and a display name that makes
/// the demo context obvious in the UI.
/// </summary>
public sealed class MockHardwareStatus : IHardwareStatus
{
    private readonly MockArduinoBridge _bridge;

    public event EventHandler? Changed;

    public bool IsConnected => _bridge.IsListening;

    public string DisplayName => _bridge.IsListening
        ? "Mock — Simulación activa"
        : "Mock — Simulación detenida";

    public MockHardwareStatus(MockArduinoBridge bridge)
    {
        _bridge = bridge;
    }
}
