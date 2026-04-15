namespace SmartParkingLot.Core;

// GRASP - Information Expert: Lectura específica de un sensor de puerta, conoce la placa y el gateId
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
