namespace SmartParkingLot.Core;

// GRASP - Controller (a nivel de usuario): Representa al administrador que gestiona el parqueadero
public class User
{
    private readonly ParkingLot _parkingLot;

    public User(ParkingLot parkingLot)
    {
        _parkingLot = parkingLot;
    }

    public void CheckAvailability()
    {
        var available = _parkingLot.AvailableSpots;
        var total = _parkingLot.TotalSpots;
        Console.WriteLine($"[User] Consulta de disponibilidad: {available}/{total} espacios disponibles");
    }

    public void ConfigSystem()
    {
        // Simulación: en un sistema real se configuraría el modo, sensores, etc.
        Console.WriteLine($"[User] Configuración del sistema — Parqueadero: {_parkingLot.Name} | Modo: {_parkingLot.Mode}");
    }
}
