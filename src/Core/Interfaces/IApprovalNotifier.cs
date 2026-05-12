using SmartParkingLot.Core.Approvals;

namespace SmartParkingLot.Core.Interfaces;

public interface IApprovalNotifier
{
    void Notify(PendingApproval approval);
}
