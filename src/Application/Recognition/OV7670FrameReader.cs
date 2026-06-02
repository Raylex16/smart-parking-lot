using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Application.Recognition;

public sealed class OV7670FrameReader : ICameraCapture
{
    private const int CaptureTimeoutMs = 10_000;

    private readonly ISerialWriter _serial;
    private readonly IEventPublisher _events;

    public OV7670FrameReader(ISerialWriter serial, IEventPublisher events)
    {
        _serial = serial;
        _events = events;
    }

    public async Task<byte[]> CaptureAsync(string gateId, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumed = 0;

        void OnFrame(CameraFrameReceived frame)
        {
            if (frame.GateId != gateId) return;
            if (Interlocked.CompareExchange(ref consumed, 1, 0) != 0) return;
            tcs.TrySetResult(frame.ImageBytes);
        }

        _events.Subscribe<CameraFrameReceived>(OnFrame);
        _serial.WriteLine($"CMD:CAM:CAPTURE:{gateId}");

        using var timeoutCts = new CancellationTokenSource(CaptureTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var timeoutTask = Task.Delay(Timeout.Infinite, linked.Token).ContinueWith(
            _ => (byte[])[], linked.Token, TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.Default);

        var winner = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);
        if (winner != tcs.Task)
        {
            ct.ThrowIfCancellationRequested();
            throw new TimeoutException($"Camera capture timeout for gate {gateId}");
        }
        return await tcs.Task.ConfigureAwait(false);
    }
}
