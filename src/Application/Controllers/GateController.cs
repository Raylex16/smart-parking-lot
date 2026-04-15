using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Application;

// GRASP - Controller: Orquesta las solicitudes de entrada/salida sin contener lógica de negocio
// GRASP - Low Coupling: Depende de interfaces (ICapacityService, IAlertService, IGate), no de implementaciones
// SOLID - DIP: Implementa IGateRequestHandler para que el dominio (Request) no dependa de esta clase concreta
public class GateController : IGateRequestHandler
{
    // Usamos un Dictionary para lookup en tiempo constante O(1)
    private readonly Dictionary<string, IGate> _gates = new();

    public ICapacityService CapacityService { get; }
    public IAlertService AlertService { get; }

    public GateController(ICapacityService capacityService, IAlertService alertService)
    {
        CapacityService = capacityService;
        AlertService = alertService;
    }

    // GRASP - Controller: Recibe el evento del sistema y delega la ejecución al Request (Polymorphism)
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
            // GRASP - Indirection: Delega la generación de alerta al servicio especializado
            var reading = new GateSensorReading("N/A", gateId);
            AlertService.GenerateAlert(reading);
            Console.WriteLine($"[GateController] ALERTA: Intento de abrir una puerta inexistente (ID: {gateId})");
        }
    }
}
