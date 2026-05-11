namespace SmartParkingLot.Core.Interfaces;

public interface IGateRequestHandler
{
    ICapacityService CapacityService { get; }
    IAlertService AlertService { get; }
    IAccessPolicy AccessPolicy { get; }
    ILogger Logger { get; }
    void OpenGate(string gateId);
    Task HandleRequestAsync(Request request);
}
