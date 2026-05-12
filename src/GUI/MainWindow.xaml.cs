using System;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Pages;
using WinRT.Interop;

namespace SmartParkingLot.Gui;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueue _ui;
    private readonly DispatcherQueueTimer _clockTimer;
    private ParkingServices? _services;

    public MainWindow()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();

        Title = "Smart Parking Lot";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TryApplyBackdrop();
        ResizeTo(1280, 800);

        _clockTimer = _ui.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
    }

    public void ShowSplash() =>
        ContentFrame.Content = new SplashPage("Iniciando servicios...");

    public void ShowFatalError(Exception ex) =>
        ContentFrame.Content = new SplashPage($"Error fatal: {ex.Message}", isError: true);

    public void OnServicesReady(ParkingServices services)
    {
        _services = services;
        LotNameText.Text = services.Lot.Name;
        UpdateOccupancyStatus();
        UpdateArduinoStatus();

        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem nvi && (string?)nvi.Tag == "dashboard")
            {
                Nav.SelectedItem = nvi;
                break;
            }
        }

        foreach (var spot in services.Lot.GetSpots())
            spot.OccupancyChanged += _ => _ui.TryEnqueue(UpdateOccupancyStatus);
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_services is null) return;

        if (args.IsSettingsSelected)
        {
            ContentFrame.Content = new SettingsPage();
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item) return;
        ContentFrame.Content = (item.Tag as string) switch
        {
            "dashboard" => new DashboardPage(_services),
            "map"       => new MapPage(_services),
            "log"       => new LogPage(_services),
            "admin"     => new AdminPage(_services),
            "hardware"  => new HardwarePage(_services),
            _ => null
        };
    }

    private void UpdateOccupancyStatus()
    {
        if (_services is null) return;
        var occ = _services.Lot.TotalSpots - _services.Lot.AvailableSpots;
        var tot = _services.Lot.TotalSpots;
        var pct = tot == 0 ? 0 : (int)Math.Round(100.0 * occ / tot);
        OccupancyStatusText.Text = $"{occ}/{tot} spots · {pct}% ocupado";
        LotStatusText.Text = $"{occ}/{tot} spots";
    }

    private void UpdateArduinoStatus()
    {
        if (_services is null) return;
        var listening = _services.Bridge.IsListening;
        ArduinoStatusText.Text = listening
            ? $"Arduino {_services.Config.Port}"
            : "Arduino — desconectado";
        ArduinoLed.Fill = (Brush)XamlApp.Current.Resources[
            listening ? "SuccessBrush" : "DangerBrush"];
    }

    public void RefreshArduinoStatus() => _ui.TryEnqueue(UpdateArduinoStatus);

    private void TryApplyBackdrop()
    {
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        else if (DesktopAcrylicController.IsSupported())
            SystemBackdrop = new DesktopAcrylicBackdrop();
    }

    private void ResizeTo(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var id = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(id);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }
}
