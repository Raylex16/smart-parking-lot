using SmartParkingLot.Core.Entities;

namespace SmartParkingLot.Core.Interfaces;

public interface IAccessPolicyConfigService
{
    Task<AccessPolicyConfig> GetAsync(string lotId, CancellationToken ct = default);
    Task UpdateWhitelistAsync(string lotId, IEnumerable<string> plates, CancellationToken ct = default);
    Task UpdateScheduleAsync(string lotId, TimeSpan start, TimeSpan end, CancellationToken ct = default);
}
