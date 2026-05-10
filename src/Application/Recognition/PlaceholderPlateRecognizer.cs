using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Recognition;

// TODO: reemplazar por CameraLicensePlateRecognizer cuando se integre la cámara LPR.
public sealed class PlaceholderPlateRecognizer : ILicensePlateRecognizer
{
    public string Recognize(string gateId) =>
        $"AUTO-{DateTime.Now:HHmmssfff}";
}
