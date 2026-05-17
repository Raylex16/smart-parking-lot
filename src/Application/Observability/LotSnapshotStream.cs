namespace SmartParkingLot.Application.Observability;

using SmartParkingLot.Application.Queries;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;

/// <summary>
/// Listens to OccupancyChanged on every ParkingSpot and refreshes the
/// current LotSnapshotDto, then fires SnapshotChanged on the calling thread.
/// Dispose() removes all subscriptions.
/// </summary>
public sealed class LotSnapshotStream : ILotSnapshotStream, IDisposable
{
    private readonly ParkingLot _lot;
    private readonly IGetLotSnapshotQuery _query;
    private readonly List<(ParkingSpot spot, Action<SpotOccupancyChanged> handler)> _subscriptions = new();
    private bool _disposed;

    public event EventHandler<LotSnapshotDto>? SnapshotChanged;

    public LotSnapshotDto Current { get; private set; }

    public LotSnapshotStream(ParkingLot lot, IGetLotSnapshotQuery query)
    {
        _lot = lot ?? throw new ArgumentNullException(nameof(lot));
        _query = query ?? throw new ArgumentNullException(nameof(query));

        // Build the initial snapshot synchronously
        Current = _query.ExecuteAsync().GetAwaiter().GetResult();

        // Subscribe to every spot already in the lot
        foreach (var spot in _lot.GetSpots())
            Subscribe(spot);
    }

    private void Subscribe(ParkingSpot spot)
    {
        Action<SpotOccupancyChanged> handler = _ => OnOccupancyChanged();
        spot.OccupancyChanged += handler;
        _subscriptions.Add((spot, handler));
    }

    private void OnOccupancyChanged()
    {
        var snapshot = _query.ExecuteAsync().GetAwaiter().GetResult();
        Current = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (spot, handler) in _subscriptions)
            spot.OccupancyChanged -= handler;

        _subscriptions.Clear();
    }
}
