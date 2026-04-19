using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using Xunit;

namespace SmartParkingLot.Tests.Application;

public class HandleSensorReadingUseCaseTests
{
    [Fact]
    public void Reading_1_marks_mapped_spot_as_occupied()
    {
        var lot = new ParkingLot("LOT", "X", ParkingMode.AUTOMATIC);
        lot.AddSpot(new ParkingSpot("A1", "zona", "std", "pb"));
        var uc = new HandleSensorReadingUseCase(lot, new Dictionary<string,string>{["IR1"]="A1"});

        uc.Handle(new SensorReadingReceived("IR1","SENSOR","1", DateTimeOffset.UtcNow));

        Assert.True(lot.GetSpots().Single(s => s.Id == "A1").IsOccupied);
    }

    [Fact]
    public void Unmapped_sensor_is_ignored()
    {
        var lot = new ParkingLot("LOT", "X", ParkingMode.AUTOMATIC);
        lot.AddSpot(new ParkingSpot("A1", "zona", "std", "pb"));
        var uc = new HandleSensorReadingUseCase(lot, new Dictionary<string,string>());

        uc.Handle(new SensorReadingReceived("IR9","SENSOR","1", DateTimeOffset.UtcNow));

        Assert.False(lot.GetSpots().Single(s => s.Id == "A1").IsOccupied);
    }
}
