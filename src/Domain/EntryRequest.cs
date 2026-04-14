namespace SmartParkingLot.Domain;

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
