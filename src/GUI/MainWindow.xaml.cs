using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Pages;
using SmartParkingLot.Gui.Resources;
using WinRT.Interop;

namespace SmartParkingLot.Gui;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueue _ui;
    private readonly DispatcherQueueTimer _clockTimer;
    private bool _servicesReady;
    private IHardwareStatus? _status;
    private ILotSnapshotStream? _lotStream;

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

    public void OnServicesReady()
    {
        _servicesReady = true;

        _status = App.Services.GetRequiredService<IHardwareStatus>();
        _status.Changed += (_, _) => _ui.TryEnqueue(UpdateArduinoStatus);

        _lotStream = App.Services.GetRequiredService<ILotSnapshotStream>();
        _lotStream.SnapshotChanged += (_, snap) => _ui.TryEnqueue(() => UpdateOccupancyFromSnapshot(snap));

        LotNameText.Text = _lotStream.Current.Name;
        UpdateOccupancyFromSnapshot(_lotStream.Current);
        UpdateArduinoStatus();

        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem nvi && (string?)nvi.Tag == "dashboard")
            {
                Nav.SelectedItem = nvi;
                break;
            }
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_servicesReady) return;

        if (args.IsSettingsSelected)
        {
            ContentFrame.Content = new SettingsPage();
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item) return;
        ContentFrame.Content = (item.Tag as string) switch
        {
            "dashboard" => App.Services.GetRequiredService<DashboardPage>(),
            "map"       => App.Services.GetRequiredService<MapPage>(),
            "log"       => App.Services.GetRequiredService<LogPage>(),
            "admin"     => App.Services.GetRequiredService<AdminPage>(),
            "hardware"  => App.Services.GetRequiredService<HardwarePage>(),
            _ => null
        };
    }

    private void UpdateOccupancyFromSnapshot(LotSnapshotDto snap)
    {
        if (!_servicesReady) return;
        var occ = snap.OccupiedSpots;
        var tot = snap.TotalSpots;
        var pct = tot == 0 ? 0 : (int)Math.Round(100.0 * occ / tot);
        OccupancyStatusText.Text = $"{occ}/{tot} spots · {pct}% ocupado";
        LotStatusText.Text = $"{occ}/{tot} spots";
    }

    private void UpdateArduinoStatus()
    {
        if (_status is null) return;
        ArduinoStatusText.Text = _status.DisplayName;
        ArduinoLed.Fill = _status.IsConnected ? AppBrushes.Success : AppBrushes.Danger;
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
