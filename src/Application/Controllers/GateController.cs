using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

/// <summary>
/// Controlador de puertas.
/// Depende SOLO de:
/// - ISpotRepository (para queries de spots)
/// - IRequestRepository (para logging de requests)
/// - No depende de métodos no usados
/// </summary>
public class GateController : IGateRequestHandler
{
    private const string LogSource = "GateController";

    private readonly Dictionary<string, IGate> _gates = new();
    private readonly ISpotRepository _spotRepository;
    private readonly IRequestRepository _requestRepository;
    private readonly ICapacityService _capacityService;
    private readonly IAlertService _alertService;
    private IAccessPolicy _policy;

    public ICapacityService CapacityService => _capacityService;
    public IAlertService AlertService => _alertService;
    public IAccessPolicy AccessPolicy => _policy;
    public ILogger Logger { get; }

    public GateController(
        ISpotRepository spotRepository,
        IRequestRepository requestRepository,
        ICapacityService capacityService,
        IAlertService alertService,
        IAccessPolicy policy,
        ILogger logger)
    {
        _spotRepository = spotRepository ?? throw new ArgumentNullException(nameof(spotRepository));
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _capacityService = capacityService ?? throw new ArgumentNullException(nameof(capacityService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleRequestAsync(Request request)
    {
        // Log la request
        await _requestRepository.LogRequestAsync(
            request.RequestId,
            request.VehiclePlate,
            request.GetType().Name,
            "LOT_ID",
            request.Timestamp,
            approved: true);

        await request.ExecuteAsync(this);
    }

    public IGate? GetGateById(string gateId)
    {
        _gates.TryGetValue(gateId, out var gate);
        return gate;
    }

    public void RegisterGate(string gateId, IGate gate)
    {
        _gates[gateId] = gate;
    }

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
