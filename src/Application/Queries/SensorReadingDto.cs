namespace SmartParkingLot.Application.Queries;

public record SensorReadingDto(string Id, string SensorId, string Value, DateTime Timestamp);

public record RequestHistoryDto(
    string RequestId,
    string VehiclePlate,
    string RequestType,
    DateTime Timestamp,
    bool Approved);

public record DeviceActionDto(string Id, string DeviceId, string Action, DateTime Timestamp);
