using SmartParkingLot.Core.Approvals;

namespace SmartParkingLot.Core.Interfaces;

public interface IApprovalQueue
{
    void Enqueue(PendingApproval approval);

    PendingApproval? TryGetById(string id);

    IReadOnlyList<PendingApproval> GetPending();

    event Action<PendingApproval>? Enqueued;
}
