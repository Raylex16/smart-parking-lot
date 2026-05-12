using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace SmartParkingLot.Gui;

public partial class App : Microsoft.UI.Xaml.Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static ParkingServices? Services { get; private set; }

    private static readonly string CrashLog =
        Path.Combine(Path.GetTempPath(), "SmartParkingLot.Gui-crash.log");

    public App()
    {
        Trace("App ctor: start");
        try
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Trace($"AppDomain unhandled: {e.ExceptionObject}");
            TaskScheduler.UnobservedTaskException += (_, e) =>
                Trace($"Task unobserved: {e.Exception}");
            Trace("App ctor: ok");
        }
        catch (Exception ex)
        {
            Trace($"App ctor FAILED: {ex}");
            throw;
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Trace("OnLaunched: start");
        try
        {
            MainWindow = new MainWindow();
            Trace("OnLaunched: MainWindow constructed");
            MainWindow.ShowSplash();
            Trace("OnLaunched: splash shown");
            MainWindow.Activate();
            Trace("OnLaunched: activated");
        }
        catch (Exception ex)
        {
            Trace($"OnLaunched WINDOW FAILED: {ex}");
            ShowMessageBox($"Error creando la ventana:\n\n{ex.Message}\n\n{CrashLog}");
            return;
        }

        try
        {
            Services = await ParkingServices.BootstrapAsync();
            Trace("OnLaunched: services ready");
            MainWindow.OnServicesReady(Services);
        }
        catch (Exception ex)
        {
            Trace($"Bootstrap FAILED: {ex}");
            MainWindow.ShowFatalError(ex);
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Trace($"UI unhandled: {e.Exception}");
        e.Handled = true;
        if (MainWindow is null)
            ShowMessageBox($"Excepción sin handler:\n\n{e.Exception?.Message}\n\nLog: {CrashLog}");
        else
            MainWindow.ShowFatalError(e.Exception!);
    }

    private static void Trace(string msg)
    {
        try
        {
            File.AppendAllText(CrashLog,
                $"[{DateTime.Now:HH:mm:ss.fff}] [tid:{Environment.CurrentManagedThreadId}] {msg}{Environment.NewLine}");
        }
        catch { }
        System.Diagnostics.Debug.WriteLine(msg);
    }

    private static void ShowMessageBox(string text)
    {
        // Win32 MessageBox so it works even when the WinUI window failed.
        MessageBoxW(IntPtr.Zero, text, "Smart Parking Lot — Fatal", 0x00000010 /* MB_ICONERROR */);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
