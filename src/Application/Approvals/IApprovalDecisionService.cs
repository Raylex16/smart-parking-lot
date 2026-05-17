namespace SmartParkingLot.Application.Approvals;

public interface IApprovalDecisionService
{
    IReadOnlyList<ApprovalDto> GetPending();
    void Resolve(string approvalId, bool approved);
}
