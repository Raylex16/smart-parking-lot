namespace SmartParkingLot.Core;

public class SensorReadingLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SensorId { get; set; }
    public string Value { get; set; }
    public DateTime Timestamp { get; set; }
}
