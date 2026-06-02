namespace SmartParkingLot.Core;

public class AlertLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
