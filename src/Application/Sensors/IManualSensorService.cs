namespace SmartParkingLot.Application.Sensors;

public interface IManualSensorService
{
    IReadOnlyList<string> SensorIds { get; }
    Task RecordSpotReadingAsync(string spotId, bool occupied, CancellationToken ct = default);
    Task TriggerGateIrAsync(string gateId, CancellationToken ct = default);
}
