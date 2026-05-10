using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

public class GateController : IGateRequestHandler
{
    private const string LogSource = "GateController";

    private readonly Dictionary<string, IGate> _gates = new();

    public ICapacityService CapacityService { get; }
    public IAlertService AlertService { get; }
    public IAccessPolicy AccessPolicy { get; }
    public ILogger Logger { get; }

    public GateController(
        ICapacityService capacityService,
        IAlertService alertService,
        IAccessPolicy accessPolicy,
        ILogger logger)
    {
        CapacityService = capacityService;
        AlertService = alertService;
        AccessPolicy = accessPolicy;
        Logger = logger;
    }

    public void HandleRequest(Request request)
    {
        request.Execute(this);
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
}
