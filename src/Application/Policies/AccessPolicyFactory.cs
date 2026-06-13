using SmartParkingLot.Core;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public interface IAccessPolicyFactory
{
    IAccessPolicy Create(ParkingMode mode, AccessPolicyConfig config);
}

public sealed class AccessPolicyFactory : IAccessPolicyFactory
{
    private readonly IApprovalQueue _queue;
    private readonly ILogger        _logger;
    private readonly TimeSpan       _manualTimeout;

    public AccessPolicyFactory(IApprovalQueue queue, ILogger logger, TimeSpan manualTimeout)
    {
        _queue         = queue;
        _logger        = logger;
        _manualTimeout = manualTimeout;
    }

    public IAccessPolicy Create(ParkingMode mode, AccessPolicyConfig config) => mode switch
    {
        ParkingMode.AUTOMATIC  => new AlwaysAllowPolicy(),
        ParkingMode.MANUAL     => new ManualAccessPolicy(_queue, _logger, _manualTimeout),
        ParkingMode.RESTRICTED => new RestrictedAccessPolicy(config.AllowedPlates, _logger),
        ParkingMode.SCHEDULED  => new ScheduledBasedPolicy(config.ScheduleStart, config.ScheduleEnd),
        _                      => new AlwaysAllowPolicy()
    };
}
