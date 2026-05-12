using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public sealed class RestrictedAccessPolicy : IAccessPolicy
{
    private const string LogSource = "RestrictedAccessPolicy";

    private readonly IReadOnlySet<string> _whitelist;
    private readonly ILogger _logger;

    public RestrictedAccessPolicy(IEnumerable<string> allowedPlates, ILogger logger)
    {
        _whitelist = new HashSet<string>(allowedPlates, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public Task<bool> CanEnterAsync(EntryRequest request)
    {
        var allowed = _whitelist.Contains(request.VehiclePlate);
        if (allowed)
            _logger.Info(LogSource, $"Placa {request.VehiclePlate} en whitelist → permitida");
        else
            _logger.Warn(LogSource, $"Placa {request.VehiclePlate} NO está en whitelist → denegada");
        return Task.FromResult(allowed);
    }
}
