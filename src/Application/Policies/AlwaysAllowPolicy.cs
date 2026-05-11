using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public class AlwaysAllowPolicy : IAccessPolicy
{
    public Task<bool> CanEnterAsync(EntryRequest request) => Task.FromResult(true);
}
