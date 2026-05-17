namespace SmartParkingLot.Gui.Infrastructure;

using Microsoft.UI.Dispatching;

public sealed class DispatcherQueueUiThreadDispatcher : IUiThreadDispatcher
{
    private readonly DispatcherQueue _queue;

    public DispatcherQueueUiThreadDispatcher(DispatcherQueue queue)
        => _queue = queue;

    public void Enqueue(Action action) => _queue.TryEnqueue(new DispatcherQueueHandler(action));
}
