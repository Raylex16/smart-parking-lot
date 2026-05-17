using SmartParkingLot.Core;

namespace SmartParkingLot.Application.Gates;

public record GateInfoDto(string GateId, GateType Type, bool IsOpen);

public record GateOperationResultDto(bool Approved, int AvailableSpots, string? Reason = null);
