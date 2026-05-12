namespace SmartParkingLot.Core.Approvals;

public sealed class PendingApproval
{
    private readonly TaskCompletionSource<bool> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Id { get; }
    public string VehiclePlate { get; }
    public string GateId { get; }
    public DateTime EnqueuedAt { get; }
    public DateTime ExpiresAt { get; }
    public Task<bool> Decision => _tcs.Task;
    public bool IsResolved => _tcs.Task.IsCompleted;

    public PendingApproval(string id, string plate, string gateId, TimeSpan ttl)
    {
        Id = id; VehiclePlate = plate; GateId = gateId;
        EnqueuedAt = DateTime.Now; ExpiresAt = EnqueuedAt + ttl;
    }

    public bool Approve() => _tcs.TrySetResult(true);
    public bool Deny()    => _tcs.TrySetResult(false);
}