using SmartParkingLot.Application.Policies;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Services;

public sealed class ParkingModeService : IParkingModeService
{
    private const string LogSource = "ParkingModeService";

    private readonly ParkingLot                    _lot;
    private readonly SwitchableAccessPolicy        _switchable;
    private readonly IParkingLotRepository         _repository;
    private readonly ILogger                       _logger;
    private readonly IAccessPolicyFactory          _factory;
    private readonly IAccessPolicyConfigRepository _configRepo;

    public ParkingMode Current => _lot.Mode;

    public ParkingModeService(
        ParkingLot lot,
        SwitchableAccessPolicy switchable,
        IParkingLotRepository repository,
        ILogger logger,
        IAccessPolicyFactory factory,
        IAccessPolicyConfigRepository configRepo)
    {
        _lot        = lot;
        _switchable = switchable;
        _repository = repository;
        _logger     = logger;
        _factory    = factory;
        _configRepo = configRepo;
    }

    public async Task SwitchToAsync(ParkingMode mode)
    {
        var config = await _configRepo.GetByLotIdAsync(_lot.Id)
                     ?? new AccessPolicyConfig(_lot.Id);

        _switchable.Set(_factory.Create(mode, config));
        _lot.SetMode(mode);
        await _repository.UpdateLotModeAsync(_lot.Id, mode).ConfigureAwait(false);
        _logger.Info(LogSource, $"Modo cambiado a {mode}");
    }
}
