namespace SmartParkingLot.Core.Events;

public sealed record SensorReadingReceived(
    string SensorId,
    string SensorType,
    string RawValue,
    DateTimeOffset Timestamp
    );
