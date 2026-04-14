namespace SmartParkingLot.Domain;

public class ParkingSpot
{
    public string Id { get; }
    public string Type { get; }
    public string Floor { get; }
    public bool IsOccupied { get; private set; }

    public ParkingSpot(string id, string type, string floor)
    {
        Id = id;
        Type = type;
        Floor = floor;
        IsOccupied = false;
    }

    public bool IsAvailable() => !IsOccupied;

    public void Occupy()
    {
        if (IsOccupied)
            throw new InvalidOperationException($"El espacio '{Id}' ya está ocupado.");
        IsOccupied = true;
    }

    public void Release()
    {
        if (!IsOccupied)
            throw new InvalidOperationException($"El espacio '{Id}' ya está libre.");
        IsOccupied = false;
    }

    public string GetStatus() => IsOccupied ? "Ocupado" : "Disponible";

    public override string ToString() =>
        $"[{Id} | Tipo: {Type} | Piso: {Floor} | Estado: {GetStatus()}]";
}
