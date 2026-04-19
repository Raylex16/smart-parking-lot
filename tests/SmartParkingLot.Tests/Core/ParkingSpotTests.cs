using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using Xunit;

namespace SmartParkingLot.Tests.Core;

public class ParkingSpotTests
{
    [Fact]
    public void ApplyOccupancy_raises_event_on_state_change()
    {
        var spot = new ParkingSpot("A1", "zona", "std", "pb");
        SpotOccupancyChanged? captured = null;
        spot.OccupancyChanged += e => captured = e;

        spot.ApplyOccupancy(true, "sensor:IR1");

        Assert.NotNull(captured);
        Assert.Equal("A1", captured!.SpotId);
        Assert.True(captured.IsOccupied);
        Assert.Equal("sensor:IR1", captured.Source);
        Assert.True(spot.IsOccupied);
    }

    [Fact]
    public void ApplyOccupancy_is_idempotent_and_does_not_raise_event()
    {
        var spot = new ParkingSpot("A1", "zona", "std", "pb");
        var raised = 0;
        spot.OccupancyChanged += _ => raised++;

        spot.ApplyOccupancy(false, "sensor"); // ya está libre
        Assert.Equal(0, raised);

        spot.ApplyOccupancy(true, "sensor");
        spot.ApplyOccupancy(true, "sensor"); // mismo estado, no dispara
        Assert.Equal(1, raised);
    }
}
