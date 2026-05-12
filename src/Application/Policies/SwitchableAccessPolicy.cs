using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public sealed class SwitchableAccessPolicy : IAccessPolicy
{
    private IAccessPolicy _current;

    public IAccessPolicy Current => _current;

    public SwitchableAccessPolicy(IAccessPolicy initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _current = initial;
    }

    public void Set(IAccessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _current = policy;
    }

    public Task<bool> CanEnterAsync(EntryRequest request) => _current.CanEnterAsync(request);
}
