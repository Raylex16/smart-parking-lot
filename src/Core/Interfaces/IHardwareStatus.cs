namespace SmartParkingLot.Core.Interfaces;

public interface IHardwareStatus
{
    bool IsConnected { get; }
    string DisplayName { get; }
    event EventHandler? Changed;
}
