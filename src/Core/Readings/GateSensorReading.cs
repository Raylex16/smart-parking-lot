namespace SmartParkingLot.Core;

public class GateSensorReading : SensorReading
{
    public string Plate { get; }
    public string GateId { get; }

    public GateSensorReading(string plate, string gateId, float registeredValue = 1.0f)
        : base(registeredValue)
    {
        Plate = plate;
        GateId = gateId;
    }

    public override string ToString() =>
        $"[GateSensorReading] Placa: {Plate} | Puerta: {GateId} | Tiempo: {Timestamp:HH:mm:ss}";
}
