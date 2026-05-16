namespace SmartParkingLot.Hardware;

/// <summary>
/// Minimal abstraction for writing lines over a serial-like channel.
/// Implemented by <see cref="ArduinoSerialBridge"/> (real hardware) and
/// <see cref="MockArduinoBridge"/> (no-op simulation).
/// </summary>
public interface ISerialWriter
{
    void WriteLine(string line);
}
