namespace SmartParkingLot.Gui.ViewModels;

public class AlertRowVm
{
    public string Message { get; init; } = "";
    public string Source { get; init; } = "";
    public string Time { get; init; } = "";
    public bool IsError { get; init; }
    public string Glyph => IsError ? "" : "";
    public string GlyphName => IsError ? "Alerta de error" : "Alerta de aviso";
}
