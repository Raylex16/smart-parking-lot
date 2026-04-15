namespace SmartParkingLot.Core;

public class ParkingSpot
{
    public string Id { get; }
    public string Address { get; }
    public string Type { get; }
    public string Floor { get; }
    public bool IsOccupied { get; private set; }

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
        $"[{Id} | Ubicación: {Address} | Tipo: {Type} | Piso: {Floor} | Estado: {GetStatus()}]";
}
