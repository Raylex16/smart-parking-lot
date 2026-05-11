namespace SmartParkingLot.Core;

public class RequestLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RequestId { get; set; }
    public string VehiclePlate { get; set; }
    public string RequestType { get; set; } // "ENTRY" o "EXIT"
    public string LotId { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Approved { get; set; }
    public string? ReleasedSpotId { get; set; }
}
