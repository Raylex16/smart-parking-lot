using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.UseCases;

public sealed class PersistSpotOccupancyOnChangeHandler
{
    private readonly IParkingRepository _repository;
    private readonly List<(ParkingSpot spot, Action<SpotOccupancyChanged> handler)> _subscriptions = new();

    public PersistSpotOccupancyOnChangeHandler(IParkingRepository repository)
    {
        _repository = repository;
    }

    public void Subscribe(IEnumerable<ParkingSpot> spots)
    {
        foreach (var spot in spots)
        {
            Action<SpotOccupancyChanged> handler = evt =>
            {
                _ = _repository.UpdateSpotStatusAsync(evt.SpotId, evt.IsOccupied);
            };
            spot.OccupancyChanged += handler;
            _subscriptions.Add((spot, handler));
        }
    }

    public void Unsubscribe()
    {
        foreach (var (spot, handler) in _subscriptions)
            spot.OccupancyChanged -= handler;
        _subscriptions.Clear();
    }
}
