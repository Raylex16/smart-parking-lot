using SmartParkingLot.Core;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public sealed class ManualAccessPolicy : IAccessPolicy
{
    private const string LogSource = "ManualAccessPolicy";

    private readonly IApprovalQueue _queue;
    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;

    public ManualAccessPolicy(IApprovalQueue queue, ILogger logger, TimeSpan timeout)
    {
        _queue = queue;
        _logger = logger;
        _timeout = timeout;
    }

    public async Task<bool> CanEnterAsync(EntryRequest request)
    {
        var id = $"APR-{Guid.NewGuid().ToString("N")[..8]}";
        var approval = new PendingApproval(id, request.VehiclePlate, request.GateId, _timeout);

        _queue.Enqueue(approval);
        _logger.Info(LogSource, $"Esperando aprobación {id} para {request.VehiclePlate} en {request.GateId} (timeout {_timeout.TotalSeconds:0}s)");

        try
        {
            var decision = await approval.Decision.WaitAsync(_timeout).ConfigureAwait(false);
            _logger.Info(LogSource, $"{id} → {(decision ? "APROBADA" : "DENEGADA")} por operario");
            return decision;
        }
        catch (TimeoutException)
        {
            approval.Deny();
            _logger.Warn(LogSource, $"{id} → DENEGADA por timeout ({_timeout.TotalSeconds:0}s)");
            return false;
        }
    }
}
