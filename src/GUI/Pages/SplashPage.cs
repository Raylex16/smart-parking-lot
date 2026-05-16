using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Gui.Resources;

namespace SmartParkingLot.Gui.Pages;

public sealed class SplashPage : Page
{
    public SplashPage(string message, bool isError = false)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 16
        };

        if (!isError)
        {
            panel.Children.Add(new ProgressRing { IsActive = true, Width = 48, Height = 48 });
        }

        var text = new TextBlock
        {
            Text = message,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (isError)
        {
            text.Foreground = AppBrushes.Danger;
            text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        panel.Children.Add(text);

        Content = panel;
    }
}

public sealed class SettingsPage : Page
{
    public SettingsPage()
    {
        var panel = new StackPanel
        {
            Padding = new Thickness(24),
            Spacing = 12
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Configuración",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Próximamente: tema, acento, idioma. El tema sigue al sistema (Configuración → Personalización → Colores).",
            Foreground = AppBrushes.Tx2
        });
        Content = panel;
    }
}
