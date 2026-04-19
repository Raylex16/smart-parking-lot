using SmartParkingLot.Application.Infrastructure;
using Xunit;

namespace SmartParkingLot.Tests.Infrastructure;

public sealed record FooEvent(int N);

public class InProcessEventBusTests
{
    [Fact]
    public void Publish_delivers_event_to_matching_subscriber()
    {
        var bus = new InProcessEventBus();
        FooEvent? received = null;
        bus.Subscribe<FooEvent>(e => received = e);

        bus.Publish(new FooEvent(42));

        Assert.Equal(new FooEvent(42), received);
    }

    [Fact]
    public void Publish_does_not_deliver_to_other_type_subscribers()
    {
        var bus = new InProcessEventBus();
        var called = false;
        bus.Subscribe<FooEvent>(_ => called = true);

        bus.Publish("no-soy-foo");

        Assert.False(called);
    }

    [Fact]
    public void Multiple_subscribers_all_receive_event()
    {
        var bus = new InProcessEventBus();
        var count = 0;
        bus.Subscribe<FooEvent>(_ => count++);
        bus.Subscribe<FooEvent>(_ => count++);

        bus.Publish(new FooEvent(1));

        Assert.Equal(2, count);
    }
}
