using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace SmartParkingLot.Gui.Resources;

/// <summary>
/// Typed wrapper over ThemeDictionaries brush keys defined in Styles/Theme.xaml.
/// Replaces direct Application.Current.Resources["XBrush"] string lookups.
/// </summary>
public static class AppBrushes
{
    // Core text brushes
    public static Brush Tx1        => Resolve("Tx1Brush");
    public static Brush Tx2        => Resolve("Tx2Brush");
    public static Brush Tx3        => Resolve("Tx3Brush");

    // Surface / layer brushes
    public static Brush Layer1     => Resolve("Layer1Brush");
    public static Brush Layer2     => Resolve("Layer2Brush");

    // Stroke brushes
    public static Brush Stroke     => Resolve("StrokeBrush");
    public static Brush Stroke2    => Resolve("Stroke2Brush");

    // Semantic state brushes
    public static Brush Success          => Resolve("SuccessBrush");
    public static Brush SuccessBackground => Resolve("SuccessBackground");
    public static Brush Warning          => Resolve("WarningBrush");
    public static Brush Danger           => Resolve("DangerBrush");
    public static Brush Accent           => Resolve("AccentBrush");

    private static Brush Resolve(string key) =>
        (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
}
