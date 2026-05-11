using SmartParkingLot.Application.Policies;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Services;

public sealed class ParkingModeService : IParkingModeService
{
    private const string LogSource = "ParkingModeService";

    private readonly ParkingLot _lot;
    private readonly SwitchableAccessPolicy _switchable;
    private readonly IParkingRepository _repository;
    private readonly ILogger _logger;
    private readonly Func<ParkingMode, IAccessPolicy> _policyFactory;

    public ParkingMode Current => _lot.Mode;

    public ParkingModeService(
        ParkingLot lot,
        SwitchableAccessPolicy switchable,
        IParkingRepository repository,
        ILogger logger,
        Func<ParkingMode, IAccessPolicy> policyFactory)
    {
        _lot = lot;
        _switchable = switchable;
        _repository = repository;
        _logger = logger;
        _policyFactory = policyFactory;
    }

    public async Task SwitchToAsync(ParkingMode mode)
    {
        if (_lot.Mode == mode)
        {
            _logger.Info(LogSource, $"Modo ya es {mode}, no se realiza cambio");
            return;
        }

        _switchable.Set(_policyFactory(mode));
        _lot.SetMode(mode);
        await _repository.UpdateLotModeAsync(_lot.Id, mode).ConfigureAwait(false);
        _logger.Info(LogSource, $"Modo cambiado a {mode}");
    }
}
