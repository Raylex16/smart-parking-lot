namespace SmartParkingLot.Core.Events;

public sealed record SpotOccupancyChanged(
    string SpotId,
    bool IsOccupied,
    string Source,
    DateTimeOffset Timestamp
    );
