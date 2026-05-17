namespace SmartParkingLot.Application.Hardware;

public record SensorInfoDto(
    string SensorId,
    string SpotId,
    string ActuatorId,
    string Address,
    string Type,
    string Floor);

public record HardwareGateInfoDto(
    string GateId,
    string IrSensorId,
    string ActuatorId,
    int Pin);

public record HardwareSnapshotDto(
    string Port,
    int BaudRate,
    bool IsConnected,
    IReadOnlyList<SensorInfoDto> Sensors,
    IReadOnlyList<HardwareGateInfoDto> Gates,
    IReadOnlyList<string> SpotSensorIds,
    string GateSensorId);
