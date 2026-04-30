using System.Collections.Concurrent;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Infrastructure;

public sealed class InProcessEventBus : IEventPublisher
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Delegate>());
        lock (list) list.Add(handler);
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : notnull
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
        Delegate[] snapshot;
        lock (list) snapshot = list.ToArray();
        foreach (var h in snapshot) ((Action<TEvent>)h)(@event);
    }
}
