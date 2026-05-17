namespace SmartParkingLot.Application.Approvals;

public record ApprovalDto(
    string Id,
    string VehiclePlate,
    string GateId,
    DateTime ExpiresAt,
    bool IsResolved,
    bool IsApproved,
    bool IsDenied);
