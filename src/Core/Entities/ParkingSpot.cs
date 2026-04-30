using SmartParkingLot.Core.Events;

namespace SmartParkingLot.Core;

public class ParkingSpot
{
    public string Id { get; }
    public string Address { get; }
    public string Type { get; }
    public string Floor { get; }
    public bool IsOccupied { get; private set; }

    public event Action<SpotOccupancyChanged>? OccupancyChanged;

    public ParkingSpot(string id, string address, string type, string floor)
    {
        Id = id;
        Address = address;
        Type = type;
        Floor = floor;
        IsOccupied = false;
    }

    public bool IsAvailable() => !IsOccupied;

    public void Occupy()
    {
        if (IsOccupied)
            throw new InvalidOperationException(string.Format(SPOT_ALREADY_OCCUPIED_MESSAGE_TEMPLATE, Id));
        IsOccupied = true;
    }

    public void Release()
    {
        if (!IsOccupied)
            throw new InvalidOperationException(string.Format(SPOT_ALREADY_AVAILABLE_MESSAGE_TEMPLATE, Id));
        IsOccupied = false;
    }

    public void ApplyOccupancy(bool isOccupied, string source)
    {
        if (IsOccupied == isOccupied) return;
        IsOccupied = isOccupied;
        OccupancyChanged?.Invoke(
            new SpotOccupancyChanged(Id, isOccupied, source, DateTimeOffset.UtcNow));
    }

    public string GetStatus() => IsOccupied ? SPOT_STATUS_OCCUPIED : SPOT_STATUS_AVAILABLE;

    public override string ToString() =>
        $"[{Id} | Ubicación: {Address} | Tipo: {Type} | Piso: {Floor} | Estado: {GetStatus()}]";
}
