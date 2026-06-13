using SmartParkingLot.Core;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Services;

public sealed class AccessPolicyConfigService : IAccessPolicyConfigService
{
    private readonly IAccessPolicyConfigRepository _repo;
    private readonly IParkingModeService           _modeService;
    private readonly ParkingLot                    _lot;

    public AccessPolicyConfigService(
        IAccessPolicyConfigRepository repo,
        IParkingModeService modeService,
        ParkingLot lot)
    {
        _repo        = repo;
        _modeService = modeService;
        _lot         = lot;
    }

    public async Task<AccessPolicyConfig> GetAsync(string lotId, CancellationToken ct = default)
    {
        var config = await _repo.GetByLotIdAsync(lotId, ct);
        if (config is not null) return config;

        var defaultConfig = new AccessPolicyConfig(lotId);
        await _repo.SaveAsync(defaultConfig, ct);
        return defaultConfig;
    }

    public async Task UpdateWhitelistAsync(string lotId, IEnumerable<string> plates, CancellationToken ct = default)
    {
        var config = await GetAsync(lotId, ct);
        config.SetWhitelist(plates);
        await _repo.SaveAsync(config, ct);
        await _modeService.SwitchToAsync(_lot.Mode);
    }

    public async Task UpdateScheduleAsync(string lotId, TimeSpan start, TimeSpan end, CancellationToken ct = default)
    {
        var config = await GetAsync(lotId, ct);
        config.SetSchedule(start, end);
        await _repo.SaveAsync(config, ct);
        await _modeService.SwitchToAsync(_lot.Mode);
    }
}
