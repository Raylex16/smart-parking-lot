namespace SmartParkingLot.Core.Interfaces;

public interface ILicensePlateRecognizer
{
    Task<string> RecognizeAsync(string gateId, CancellationToken ct = default);
}
