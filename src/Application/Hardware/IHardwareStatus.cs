namespace SmartParkingLot.Application.Hardware;

public interface IHardwareStatus
{
    bool IsConnected { get; }
    string DisplayName { get; }
    event EventHandler? Changed;
}
