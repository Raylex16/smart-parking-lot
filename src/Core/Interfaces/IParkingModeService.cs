namespace SmartParkingLot.Core.Interfaces;

public interface IParkingModeService
{
    ParkingMode Current { get; }

    Task SwitchToAsync(ParkingMode mode);
}
