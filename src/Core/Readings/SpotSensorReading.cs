namespace SmartParkingLot.Core;

// GRASP - Information Expert: Lectura específica de un sensor de espacio, sabe si está ocupado
public class SpotSensorReading : SensorReading
{
    public string SpotId { get; }
    public bool IsOccupied { get; }

    public SpotSensorReading(string spotId, bool isOccupied)
        : base(isOccupied ? 1.0f : 0.0f)
    {
        SpotId = spotId;
        IsOccupied = isOccupied;
    }

    public override string ToString() =>
        $"[SpotSensorReading] Espacio: {SpotId} | Ocupado: {(IsOccupied ? "Sí" : "No")} | Tiempo: {Timestamp:HH:mm:ss}";
}
