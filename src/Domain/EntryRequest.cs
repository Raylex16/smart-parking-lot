namespace SmartParkingLot.Domain;

/// <summary>
/// Representa el evento de sistema: un vehículo solicita entrar al parqueadero.
/// </summary>
/// <remarks>
/// GRASP - Controller (soporte):
/// EntryRequest es el objeto que encapsula el evento del sistema.
/// El GateController lo recibe y lo procesa; así el evento queda bien
/// definido como un objeto de dominio y no como parámetros sueltos.
/// </remarks>
public class EntryRequest
{
    public string VehicleId { get; }
    public string VehicleType { get; }
    public DateTime Timestamp { get; }

    public EntryRequest(string vehicleId, string vehicleType)
    {
        VehicleId = vehicleId;
        VehicleType = vehicleType;
        Timestamp = DateTime.Now;
    }

    public override string ToString() =>
        $"EntryRequest {{ VehicleId={VehicleId}, Type={VehicleType}, At={Timestamp:HH:mm:ss} }}";
}
