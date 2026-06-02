using System.Runtime.InteropServices;
using SmartParkingLot.Core.Interfaces;
using Tesseract;

namespace SmartParkingLot.Application.Recognition;

public sealed class TesseractPlateRecognizer : ILicensePlateRecognizer
{
    private const int FrameWidth = 160;
    private const int FrameHeight = 120;
    private const string CharWhitelist = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";

    private readonly ICameraCapture _camera;
    private readonly string _tessDataPath;

    public TesseractPlateRecognizer(ICameraCapture camera, string tessDataPath)
    {
        _camera = camera;
        _tessDataPath = tessDataPath;
    }

    public async Task<string> RecognizeAsync(string gateId, CancellationToken ct = default)
    {
        var bytes = await _camera.CaptureAsync(gateId, ct).ConfigureAwait(false);
        if (bytes.Length == 0) return "UNKNOWN";

        using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", CharWhitelist);

        using var pix = BuildPix(bytes);
        using var page = engine.Process(pix);
        var plate = page.GetText().Trim();
        return string.IsNullOrWhiteSpace(plate) ? "UNKNOWN" : plate;
    }

    // Builds an 8bpp grayscale Leptonica Pix from raw Y-channel bytes.
    // Leptonica stores 8bpp rows as packed bytes with no padding when width
    // is a multiple of 4 (160 is divisible by 4), so a single Marshal.Copy works.
    private static Pix BuildPix(byte[] grayscaleBytes)
    {
        var pix = Pix.Create(FrameWidth, FrameHeight, 8);
        var pixData = pix.GetData();
        Marshal.Copy(grayscaleBytes, 0, pixData.Data, grayscaleBytes.Length);
        return pix;
    }
}
