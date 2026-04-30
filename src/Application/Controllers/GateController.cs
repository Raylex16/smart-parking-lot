using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application;

public class GateController : IGateRequestHandler
{
    private readonly Dictionary<string, IGate> _gates = new();

    public ICapacityService CapacityService { get; }
    public IAlertService AlertService { get; }

    public GateController(ICapacityService capacityService, IAlertService alertService)
    {
        CapacityService = capacityService;
        AlertService = alertService;
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
            var reading = new GateSensorReading("N/A", gateId);
            AlertService.GenerateAlert(reading);
            Console.WriteLine($"[GateController] ALERTA: Intento de abrir una puerta inexistente (ID: {gateId})");
        }
    }
}
