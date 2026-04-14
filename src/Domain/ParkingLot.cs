namespace SmartParkingLot.Domain;

public class ParkingLot
{
    public string Id { get; }
    public string Name { get; }

    private readonly List<ParkingSpot> _spots;

    public int TotalSpots => _spots.Count;
    public int AvailableSpots => _spots.Count(s => s.IsAvailable());

    public ParkingLot(string id, string name)
    {
        Id = id;
        Name = name;
        _spots = [];
    }

    public void AddSpot(ParkingSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        _spots.Add(spot);
    }

    public void RemoveSpot(string spotId)
    {
        var spot = _spots.FirstOrDefault(s => s.Id == spotId);
        if (spot is not null) _spots.Remove(spot);
    }

    public bool IsAvailable() => AvailableSpots > 0;

    public ParkingSpot? GetAvailableSpot() =>
        _spots.FirstOrDefault(s => s.IsAvailable());

    public IReadOnlyList<ParkingSpot> GetSpots() => _spots.AsReadOnly();
}
