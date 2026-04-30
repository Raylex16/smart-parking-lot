namespace SmartParkingLot.Core.Interfaces;

public interface IGateRequestHandler
{
    ICapacityService CapacityService { get; }
    IAlertService AlertService { get; }
    void OpenGate(string gateId);
}
