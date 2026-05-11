using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Handlers;

public sealed class LcdCapacityHandler
{
    private readonly ParkingLot _lot;
    private readonly IDisplay _display;

    public LcdCapacityHandler(ParkingLot lot, IDisplay display)
    {
        _lot = lot;
        _display = display;
    }

    public void Handle(SpotOccupancyChanged evt)
    {
        _display.ShowCapacity(_lot.AvailableSpots, _lot.TotalSpots);
    }
}
