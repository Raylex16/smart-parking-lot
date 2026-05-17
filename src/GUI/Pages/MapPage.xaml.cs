using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class MapPage : Page
{
    public MapPageViewModel ViewModel { get; }

    public MapPage(MapPageViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Activate();
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Deactivate();

    private async void OnEntryClick(object sender, RoutedEventArgs e)
    {
        var plate = await PromptForPlateAsync("Solicitar entrada", "Placa del vehículo:");
        if (string.IsNullOrWhiteSpace(plate)) return;
        var (approved, avail) = await ViewModel.RequestEntryAsync(plate);
        await ShowResultDialog(approved
            ? $"✓ Entrada CONCEDIDA para {plate}.\nDisponibles: {avail}"
            : $"✗ Entrada DENEGADA para {plate}.\nDisponibles: {avail}");
    }

    private async void OnExitClick(object sender, RoutedEventArgs e)
    {
        var plate = await PromptForPlateAsync("Solicitar salida", "Placa del vehículo:");
        if (string.IsNullOrWhiteSpace(plate)) return;
        var (_, _) = await ViewModel.RequestExitAsync(plate);
        await ShowResultDialog($"✓ Puerta de salida abierta para {plate}.\n" +
                               "La liberación del spot la detecta el sensor.");
    }

    private async Task<string> PromptForPlateAsync(string title, string label)
    {
        var input = new TextBox { PlaceholderText = "ABC-123", MinWidth = 240 };
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "Aceptar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { new TextBlock { Text = label }, input }
            }
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? input.Text.Trim() : string.Empty;
    }

    private async Task ShowResultDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Resultado",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Ok",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }
}
