using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Infrastructure;

public static class AsyncEventSubscriber
{
    public static void SubscribeAsync<TEvent>(
        this IEventPublisher bus,
        Func<TEvent, Task> handler,
        ILogger logger,
        string source)
        where TEvent : notnull
    {
        bus.Subscribe<TEvent>(evt => _ = SafeFireAsync(handler, evt, logger, source));
    }

    private static async Task SafeFireAsync<TEvent>(
        Func<TEvent, Task> handler,
        TEvent evt,
        ILogger logger,
        string source)
    {
        try
        {
            await handler(evt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, source, $"Handler async lanzó excepción: {ex}");
        }
    }
}
