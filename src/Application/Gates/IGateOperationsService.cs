namespace SmartParkingLot.Application.Gates;

public interface IGateOperationsService
{
    IReadOnlyList<GateInfoDto> GetRegisteredGates();
    Task<GateOperationResultDto> RequestEntryAsync(string plate, string gateId, CancellationToken ct = default);
    Task<GateOperationResultDto> RequestExitAsync(string plate, string gateId, CancellationToken ct = default);
    Task OpenAsync(string gateId, CancellationToken ct = default);
    Task CloseAsync(string gateId, CancellationToken ct = default);
}
