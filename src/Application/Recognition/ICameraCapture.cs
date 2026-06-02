namespace SmartParkingLot.Application.Recognition;

public interface ICameraCapture
{
    Task<byte[]> CaptureAsync(string gateId, CancellationToken ct = default);
}
