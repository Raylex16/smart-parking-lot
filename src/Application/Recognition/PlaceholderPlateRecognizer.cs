using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Recognition;

public sealed class PlaceholderPlateRecognizer : ILicensePlateRecognizer
{
    public Task<string> RecognizeAsync(string gateId, CancellationToken ct = default)
        => Task.FromResult($"AUTO-{DateTime.Now:HHmmssfff}");
}
