using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SmartParkingLot.Gui.Controls;

public enum BadgeKind { Success, Warning, Danger, Neutral, Accent }

/// Factory for the Fluent-style chips used across the app.
/// Border is sealed in WinUI 3 — we return a fully-styled instance.
public static class Badge
{
    public static Border New(string text, BadgeKind kind = BadgeKind.Neutral)
    {
        var (bg, fg) = ResolveColors(kind);
        return new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(7, 1, 7, 1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                CharacterSpacing = 40,
                Foreground = fg
            }
        };
    }

    private static (Brush bg, Brush fg) ResolveColors(BadgeKind kind)
    {
        var res = Microsoft.UI.Xaml.Application.Current.Resources;
        return kind switch
        {
            BadgeKind.Success => ((Brush)res["SuccessBackground"], (Brush)res["SuccessBrush"]),
            BadgeKind.Warning => ((Brush)res["WarningBackground"], (Brush)res["WarningBrush"]),
            BadgeKind.Danger  => ((Brush)res["DangerBackground"],  (Brush)res["DangerBrush"]),
            BadgeKind.Accent  => ((Brush)res["AccentLightBrush"],  (Brush)res["AccentFillColorDefaultBrush"]),
            _ => ((Brush)res["NeutralBackground"], (Brush)res["NeutralBrush"]),
        };
    }
}
