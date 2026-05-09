namespace SmartParkingLot.Core.Interfaces;

public interface IArduinoReader : IDisposable
{
    void StartListening();
    void StopListening();
    bool IsListening { get; }
}
