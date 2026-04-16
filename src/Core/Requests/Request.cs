using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Core;

public abstract class Request
{
    public string GateId { get; init; } = "G-01";
    public string VehiclePlate { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;

    // GRASP - Polymorphism: Cada tipo de Request implementa su propia lógica de ejecución
    // SOLID - DIP: Depende de la abstracción IGateRequestHandler, no del GateController concreto
    public abstract void Execute(IGateRequestHandler handler);
}
