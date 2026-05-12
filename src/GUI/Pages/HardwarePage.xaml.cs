using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Controls;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class HardwarePage : Page
{
    private const int FILE_LOG_TAIL_LINES = 80;

    private readonly ParkingServices _svc;
    private readonly DispatcherQueue _ui;
    private Action<LogEntry>? _logHandler;

    public HardwarePage(ParkingServices svc)
    {
        InitializeComponent();
        _svc = svc;
        _ui = DispatcherQueue.GetForCurrentThread();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PortBox.Text = _svc.Config.Port;
        BaudBox.Text = _svc.Config.BaudRate.ToString();

        SensorCombo.Items.Clear();
        SensorCombo.Items.Add(_svc.GateSensor.Id);
        foreach (var s in _svc.SpotSensors.Values)
            SensorCombo.Items.Add(s.Id);
        SensorCombo.SelectedIndex = 0;

        // Seed log panel with current snapshot
        foreach (var entry in _svc.UiLogger.Snapshot())
            AppendLog(entry, autoScroll: false);

        // Subscribe to new entries
        _logHandler = entry => _ui.TryEnqueue(() => AppendLog(entry, autoScroll: AutoScrollToggle.IsOn));
        _svc.UiLogger.Appended += _logHandler;

        RefreshConnectionState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_logHandler is not null)
            _svc.UiLogger.Appended -= _logHandler;
    }

    private void RefreshConnectionState()
    {
        var listening = _svc.Bridge.IsListening;
        ConnBadgeHost.Content = Badge.New(listening ? "Conectado" : "Desconectado",
            listening ? BadgeKind.Success : BadgeKind.Danger);

        ConnectBtn.Content = BuildButtonContent(
            listening ? "" : "",
            listening ? "Desconectar" : "Conectar");
        if (App.MainWindow != null)
            App.MainWindow.RefreshArduinoStatus();
    }

    private static StackPanel BuildButtonContent(string glyph, string label)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        stack.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = glyph,
            FontSize = 12
        });
        stack.Children.Add(new TextBlock { Text = label });
        return stack;
    }

    private void OnToggleConnectionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_svc.Bridge.IsListening)
                _svc.Bridge.StopListening();
            else
                _svc.Bridge.StartListening();
        }
        catch (Exception ex)
        {
            _svc.UiLogger.Log(LogLevel.Error, "HardwarePage", $"No se pudo cambiar el estado: {ex.Message}");
        }
        RefreshConnectionState();
    }

    private void OnScanClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            PortsList.Text = ports.Length == 0
                ? "Sin puertos seriales detectados."
                : "Puertos disponibles: " + string.Join(", ", ports);
        }
        catch (Exception ex)
        {
            PortsList.Text = $"Error: {ex.Message}";
        }
    }

    private void OnClearLogClick(object sender, RoutedEventArgs e) => LogPanel.Children.Clear();

    private void OnEmitClick(object sender, RoutedEventArgs e)
    {
        var sensorId = SensorCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(sensorId)) return;

        var raw = (StateCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "0";
        var type = sensorId.StartsWith("SEN-SPOT-") ? "Ultrasonido" : "LPR";

        _svc.Bus.Publish(new SensorReadingReceived(
            SensorId: sensorId,
            SensorType: type,
            RawValue: raw,
            Timestamp: DateTimeOffset.Now));

        _svc.UiLogger.Log(LogLevel.Info, "Manual",
            $"Publicado: {sensorId} = {raw}");
    }

    private void OnSimEntryIrClick(object sender, RoutedEventArgs e)
    {
        _svc.Bus.Publish(new SensorReadingReceived("GATE-IR1", "IR", "1", DateTimeOffset.Now));
        _svc.UiLogger.Log(LogLevel.Info, "Manual", "Simulado: GATE-IR1 = 1");
    }

    private void OnSimExitIrClick(object sender, RoutedEventArgs e)
    {
        _svc.Bus.Publish(new SensorReadingReceived("GATE-IR2", "IR", "1", DateTimeOffset.Now));
        _svc.UiLogger.Log(LogLevel.Info, "Manual", "Simulado: GATE-IR2 = 1");
    }

    private void OnLoadFileLogClick(object sender, RoutedEventArgs e)
    {
        var path = _svc.FileLogger.GetCurrentLogFilePath();
        LogFilePath.Text = path;

        if (!File.Exists(path))
        {
            LogFileText.Text = "No hay archivo de log para hoy.";
            return;
        }

        var tail = new Queue<string>(FILE_LOG_TAIL_LINES);
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (tail.Count == FILE_LOG_TAIL_LINES) tail.Dequeue();
                tail.Enqueue(line);
            }
        }
        catch (Exception ex)
        {
            LogFileText.Text = $"Error leyendo log: {ex.Message}";
            return;
        }

        LogFileText.Text = string.Join("\n", tail);
    }

    private void AppendLog(LogEntry entry, bool autoScroll)
    {
        var color = entry.Level switch
        {
            LogLevel.Error   => (Brush)XamlApp.Current.Resources["DangerBrush"],
            LogLevel.Warning => (Brush)XamlApp.Current.Resources["WarningBrush"],
            LogLevel.Info    => (Brush)XamlApp.Current.Resources["Tx1Brush"],
            _ => (Brush)XamlApp.Current.Resources["Tx3Brush"]
        };
        LogPanel.Children.Add(new TextBlock
        {
            Text = entry.Formatted,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 11,
            Foreground = color
        });

        // Cap UI count to avoid runaway memory
        while (LogPanel.Children.Count > 500)
            LogPanel.Children.RemoveAt(0);

        if (autoScroll)
            LogScroll.ChangeView(null, double.MaxValue, null, true);
    }
}
