namespace SmartParkingLot.Core;

public class User
{
    private readonly ParkingLot _parkingLot;

    public User(ParkingLot parkingLot)
    {
        _parkingLot = parkingLot;
    }

    public (int Available, int Total) GetAvailability() =>
        (_parkingLot.AvailableSpots, _parkingLot.TotalSpots);

    public (string Name, ParkingMode Mode) GetSystemConfig() =>
        (_parkingLot.Name, _parkingLot.Mode);
}
