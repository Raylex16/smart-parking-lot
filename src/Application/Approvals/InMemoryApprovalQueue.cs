using System.Collections.Concurrent;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Approvals;

public sealed class InMemoryApprovalQueue : IApprovalQueue
{
    private readonly ConcurrentDictionary<string, PendingApproval> _pending = new();

    public event Action<PendingApproval>? Enqueued;

    public void Enqueue(PendingApproval approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        _pending[approval.Id] = approval;
        Enqueued?.Invoke(approval);
    }

    public PendingApproval? TryGetById(string id)
    {
        return _pending.TryGetValue(id, out var approval) ? approval : null;
    }

    public IReadOnlyList<PendingApproval> GetPending()
    {
        var snapshot = _pending.Values.ToList();
        foreach (var resolved in snapshot.Where(a => a.IsResolved))
            _pending.TryRemove(resolved.Id, out _);

        return snapshot.Where(a => !a.IsResolved).ToList();
    }
}
