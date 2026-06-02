namespace SmartParkingLot.Core;

public class DeviceActionLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
}
