using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using Xunit;

namespace SmartParkingLot.Tests.Application;

public class SpotOccupancyChangedHandlerTests
{
    sealed class FakeDispatcher : ICommandDispatcher
    {
        public List<ActuatorCommand> Sent { get; } = new();
        public void Dispatch(ActuatorCommand c) => Sent.Add(c);
    }

    [Fact]
    public void Occupied_spot_sends_LED_ON()
    {
        var fake = new FakeDispatcher();
        var h = new SpotOccupancyChangedHandler(fake, new Dictionary<string,string>{["A1"]="LED1"});

        h.Handle(new SpotOccupancyChanged("A1", true, "sensor:IR1", DateTimeOffset.UtcNow));

        var cmd = Assert.Single(fake.Sent);
        Assert.Equal("LED1", cmd.ActuatorId);
        Assert.Equal("SET", cmd.Action);
        Assert.Equal("1", cmd.Payload);
    }

    [Fact]
    public void Free_spot_sends_LED_OFF()
    {
        var fake = new FakeDispatcher();
        var h = new SpotOccupancyChangedHandler(fake, new Dictionary<string,string>{["A1"]="LED1"});

        h.Handle(new SpotOccupancyChanged("A1", false, "sensor:IR1", DateTimeOffset.UtcNow));

        Assert.Equal("0", fake.Sent.Single().Payload);
    }
}
