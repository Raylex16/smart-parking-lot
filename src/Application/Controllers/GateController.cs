using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

/// <summary>
/// Controlador de puertas. Orquesta el dominio (capacidad, política de acceso,
/// alertas) y no realiza persistencia: el logging de requests es responsabilidad
/// de <see cref="Gates.GateOperationsService"/>, que conoce el LotId correcto.
/// </summary>
public class GateController : IGateRequestHandler
{
    private const string LogSource = "GateController";

    private readonly Dictionary<string, IGate> _gates = new();
    private readonly ICapacityService _capacityService;
    private readonly IAlertService _alertService;
    private IAccessPolicy _policy;

    public ICapacityService CapacityService => _capacityService;
    public IAlertService AlertService => _alertService;
    public IAccessPolicy AccessPolicy => _policy;
    public ILogger Logger { get; }

    public GateController(
        ICapacityService capacityService,
        IAlertService alertService,
        IAccessPolicy policy,
        ILogger logger)
    {
        _capacityService = capacityService ?? throw new ArgumentNullException(nameof(capacityService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task HandleRequestAsync(Request request) => request.ExecuteAsync(this);

    public IGate? GetGateById(string gateId)
    {
        _gates.TryGetValue(gateId, out var gate);
        return gate;
    }

    public void RegisterGate(string gateId, IGate gate)
    {
        _gates[gateId] = gate;
    }

    public IReadOnlyDictionary<string, IGate> GetRegisteredGates() =>
        _gates.AsReadOnly();

    public void OpenGate(string gateId)
    {
        var gate = GetGateById(gateId);

        if (gate is not null)
        {
            gate.Open();
        }
        else
        {
            AlertService.GenerateAlert(new GateSensorReading("N/A", gateId));
            Logger.Warn(LogSource, $"Intento de abrir una puerta inexistente (ID: {gateId})");
        }
    }

    public void SetAccessPolicy(IAccessPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }
}
