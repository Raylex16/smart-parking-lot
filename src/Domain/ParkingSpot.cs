namespace SmartParkingLot.Domain;

/// <summary>
/// Representa un espacio físico dentro del estacionamiento.
/// </summary>
/// <remarks>
/// GRASP - Information Expert:
/// ParkingSpot es el experto natural de su propio estado (ocupado/disponible).
/// Nadie más debería modificar IsOccupied directamente; toda la lógica
/// de transición vive aquí, donde reside la información.
/// </remarks>
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

    // GRASP - Information Expert: ParkingSpot sabe si está disponible.
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

    // GRASP - Information Expert: ParkingSpot produce su propia descripción de estado.
    public string GetStatus() => IsOccupied ? "Ocupado" : "Disponible";

    public override string ToString() =>
        $"[{Id} | Tipo: {Type} | Piso: {Floor} | Estado: {GetStatus()}]";
}
