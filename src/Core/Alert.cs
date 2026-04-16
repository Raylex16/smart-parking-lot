namespace SmartParkingLot.Core;

// GRASP - Information Expert: La alerta conoce su contenido y sabe cómo notificarse
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
        // Simulación: en un sistema real se enviaría a un canal de notificaciones
        Console.WriteLine($"[Alert {Id}] [{Type}] {Message} (Fecha: {Date:yyyy-MM-dd HH:mm:ss})");
    }

    public override string ToString() =>
        $"[Alert {Id} | {Type}] {Message}";
}
