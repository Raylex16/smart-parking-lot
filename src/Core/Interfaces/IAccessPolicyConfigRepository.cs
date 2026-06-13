using SmartParkingLot.Core.Entities;

namespace SmartParkingLot.Core.Interfaces;

public interface IAccessPolicyConfigRepository
{
    Task<AccessPolicyConfig?> GetByLotIdAsync(string lotId, CancellationToken ct = default);
    Task SaveAsync(AccessPolicyConfig config, CancellationToken ct = default);
}
