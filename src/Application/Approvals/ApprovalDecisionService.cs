using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Approvals;

public sealed class ApprovalDecisionService : IApprovalDecisionService
{
    private readonly IApprovalQueue _queue;

    public ApprovalDecisionService(IApprovalQueue queue)
    {
        _queue = queue;
    }

    public IReadOnlyList<ApprovalDto> GetPending() =>
        _queue.GetPending()
            .Select(a => new ApprovalDto(
                a.Id, a.VehiclePlate, a.GateId, a.ExpiresAt,
                a.IsResolved,
                IsApproved: a.IsResolved && a.Decision.IsCompletedSuccessfully && a.Decision.Result,
                IsDenied:   a.IsResolved && a.Decision.IsCompletedSuccessfully && !a.Decision.Result))
            .ToList()
            .AsReadOnly();

    public void Resolve(string approvalId, bool approved)
    {
        var approval = _queue.TryGetById(approvalId)
            ?? throw new InvalidOperationException($"Aprobación '{approvalId}' no encontrada.");

        if (approval.IsResolved)
            throw new InvalidOperationException($"Aprobación '{approvalId}' ya fue resuelta.");

        if (approved)
            approval.Approve();
        else
            approval.Deny();
    }
}
