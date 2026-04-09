namespace SmartParkingLot.Domain;

/// <summary>
/// Agrega y administra todos los espacios del estacionamiento.
/// </summary>
/// <remarks>
/// GRASP - Information Expert:
/// ParkingLot es el experto de la colección de espacios; es quien tiene
/// la información necesaria para responder cuántos espacios hay, cuántos
/// están disponibles y cuál es el primero libre. Ninguna clase externa
/// necesita iterar la lista interna.
///
/// GRASP - Low Coupling:
/// ParkingLot no conoce ni a GateController ni a CapacityService.
/// Solo expone comportamiento sobre sus propios datos.
/// </remarks>
public class ParkingLot
{
    public string Id { get; }
    public string Name { get; }

    private readonly List<ParkingSpot> _spots;

    // GRASP - Information Expert: calcula totales a partir de la propia colección.
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

    // GRASP - Information Expert: ParkingLot es quien sabe si tiene capacidad.
    public bool IsAvailable() => AvailableSpots > 0;

    /// <summary>Retorna el primer espacio libre, o null si no hay ninguno.</summary>
    public ParkingSpot? GetAvailableSpot() =>
        _spots.FirstOrDefault(s => s.IsAvailable());

    /// <summary>Vista de solo lectura de todos los espacios (para reportes/UI).</summary>
    public IReadOnlyList<ParkingSpot> GetSpots() => _spots.AsReadOnly();
}
