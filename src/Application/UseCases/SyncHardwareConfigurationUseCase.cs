using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.UseCases;

public sealed class SyncHardwareConfigurationUseCase
{
    private readonly HardwareConfig _config;
    private readonly IParkingRepository _repository;
    private readonly ILogger _logger;
    private readonly string _lotId;

    public SyncHardwareConfigurationUseCase(
        HardwareConfig config,
        IParkingRepository repository,
        ILogger logger,
        string lotId)
    {
        _config     = config;
        _repository = repository;
        _logger     = logger;
        _lotId      = lotId;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        foreach (var mapping in _config.Sensors)
            await _repository.EnsureSpotExistsAsync(
                mapping.SpotId, _lotId,
                mapping.Address, mapping.Type, mapping.Floor, ct);

        var validSpotIds = _config.Sensors.Select(m => m.SpotId).ToList();
        var removed = await _repository.RemoveOrphanSpotsAsync(_lotId, validSpotIds, ct);
        if (removed > 0)
            _logger.Info("SyncHardwareConfig", $"Eliminados {removed} spot(s) huérfanos no presentes en hardware.json");
    }
}
