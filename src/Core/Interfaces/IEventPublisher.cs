namespace SmartParkingLot.Core.Interfaces;

public interface IEventPublisher
{
    void Publish<TEvent>(TEvent @event) where TEvent : notnull;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
}
