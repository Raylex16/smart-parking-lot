namespace SmartParkingLot.Core.Commands;

public sealed record ActuatorCommand(
    string CommandId,
    string ActuatorId,
    string Action,
    string Payload);
