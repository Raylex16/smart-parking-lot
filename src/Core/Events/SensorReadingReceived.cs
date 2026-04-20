namespace SmartParkingLot.Core.Events;

// GRASP - Pure Fabrication: contrato inbound desacoplado de transporte serial.
public sealed record SensorReadingReceived(
    string SensorId,
    string SensorType,
    string RawValue,
    DateTimeOffset Timestamp
    );
