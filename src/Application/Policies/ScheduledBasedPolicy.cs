using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public class ScheduledBasedPolicy : IAccessPolicy
{
    private readonly TimeSpan _startTime;
    private readonly TimeSpan _endTime;

    public ScheduledBasedPolicy(TimeSpan startTime, TimeSpan endTime)
    {
        _startTime = startTime;
        _endTime = endTime;
    }

    public Task<bool> CanEnterAsync(EntryRequest request)
    {
        var currentTime = DateTime.Now.TimeOfDay;
        return Task.FromResult(currentTime >= _startTime && currentTime <= _endTime);
    }
}
