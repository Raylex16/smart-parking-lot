namespace SmartParkingLot.Core.Ports;

// GRASP - Indirection: Interfaz que desacopla los Request del controlador concreto
// SOLID - Dependency Inversion Principle (DIP): Las entidades de dominio dependen
// de esta abstracción, no de la implementación concreta (GateController).
// Esto permite que el módulo Core no tenga dependencia alguna hacia Application.
public interface IGateRequestHandler
{
    /// <summary>Servicio de capacidad para consultar y reservar espacios.</summary>
    ICapacityService CapacityService { get; }

    /// <summary>Servicio de alertas para notificar eventos anómalos.</summary>
    IAlertService AlertService { get; }

    /// <summary>Abre la puerta identificada por <paramref name="gateId"/>.</summary>
    void OpenGate(string gateId);
}
