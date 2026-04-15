namespace SmartParkingLot.Core;

// GRASP - Information Expert: La lectura del sensor es quien conoce su timestamp y valor registrado
public abstract class SensorReading
{
    public DateTime Timestamp { get; }
    public float RegisteredValue { get; }

    protected SensorReading(float registeredValue)
    {
        Timestamp = DateTime.Now;
        RegisteredValue = registeredValue;
    }

    public override string ToString() =>
        $"[SensorReading] Valor: {RegisteredValue} | Tiempo: {Timestamp:HH:mm:ss}";
}
