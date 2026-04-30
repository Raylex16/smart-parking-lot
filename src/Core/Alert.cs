namespace SmartParkingLot.Core;

public class Alert
{
    public string Id { get; }
    public string Type { get; }
    public string Message { get; }
    public DateTime Date { get; }

    public Alert(string id, string type, string message)
    {
        Id = id;
        Type = type;
        Message = message;
        Date = DateTime.Now;
    }

    public void Notify()
    {
        Console.WriteLine($"[Alert {Id}] [{Type}] {Message} (Fecha: {Date:yyyy-MM-dd HH:mm:ss})");
    }

    public override string ToString() =>
        $"[Alert {Id} | {Type}] {Message}";
}
