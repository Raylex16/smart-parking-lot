using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Infrastructure;
using SmartParkingLot.Gui.Resources;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Gui.ViewModels;

public partial class HardwarePageViewModel : ObservableObject
{
    private const int FILE_LOG_TAIL_LINES = 80;

    private readonly IHardwareStatus _hardwareStatus;
    private readonly IEventPublisher _bus;
    private readonly GuiLogger _uiLogger;
    private readonly FileLogger _fileLogger;
    private readonly ArduinoSerialBridge _bridge;
    private readonly HardwareConfig _config;
    private readonly IReadOnlyDictionary<string, Sensor<SpotSensorReading>> _spotSensors;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly IUiThreadDispatcher _ui;

    private Action<LogEntry>? _logHandler;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionLabel = "Desconectado";
    [ObservableProperty] private string _connectButtonGlyph = "";  // plug icon
    [ObservableProperty] private string _connectButtonLabel = "Conectar";
    [ObservableProperty] private string _configPort = "";
    [ObservableProperty] private string _configBaudRate = "";
    [ObservableProperty] private string _portsListText = "";
    [ObservableProperty] private string _logFilePath = "";
    [ObservableProperty] private string _logFileText = "";
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private string _selectedSensorId = "";
    [ObservableProperty] private string _selectedStateValue = "1";

    public ObservableCollection<string> SensorIds { get; } = new();
    public ObservableCollection<LogLineVm> LogLines { get; } = new();

    public HardwarePageViewModel(
        IHardwareStatus hardwareStatus,
        IEventPublisher bus,
        GuiLogger uiLogger,
        FileLogger fileLogger,
        ArduinoSerialBridge bridge,
        HardwareConfig config,
        IReadOnlyDictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        IUiThreadDispatcher ui)
    {
        _hardwareStatus  = hardwareStatus;
        _bus             = bus;
        _uiLogger        = uiLogger;
        _fileLogger      = fileLogger;
        _bridge          = bridge;
        _config          = config;
        _spotSensors     = spotSensors;
        _gateSensor      = gateSensor;
        _ui              = ui;
    }

    public void Activate()
    {
        ConfigPort     = _config.Port;
        ConfigBaudRate = _config.BaudRate.ToString();

        SensorIds.Clear();
        SensorIds.Add(_gateSensor.Id);
        foreach (var s in _spotSensors.Values)
            SensorIds.Add(s.Id);
        if (SensorIds.Count > 0)
            SelectedSensorId = SensorIds[0];

        foreach (var entry in _uiLogger.Snapshot())
            AppendLogEntry(entry);

        _logHandler = entry => _ui.Enqueue(() => AppendLogEntry(entry));
        _uiLogger.Appended += _logHandler;

        RefreshConnectionState();
    }

    public void Deactivate()
    {
        if (_logHandler is not null)
            _uiLogger.Appended -= _logHandler;
    }

    private void RefreshConnectionState()
    {
        IsConnected        = _bridge.IsListening;
        ConnectionLabel    = _bridge.IsListening ? "Conectado" : "Desconectado";
        ConnectButtonGlyph = _bridge.IsListening ? "" : "";
        ConnectButtonLabel = _bridge.IsListening ? "Desconectar" : "Conectar";
    }

    [RelayCommand]
    private void ToggleConnection()
    {
        try
        {
            if (_bridge.IsListening)
                _bridge.StopListening();
            else
                _bridge.StartListening();
        }
        catch (Exception ex)
        {
            _uiLogger.Log(LogLevel.Error, "HardwarePage", $"No se pudo cambiar el estado: {ex.Message}");
        }
        RefreshConnectionState();
    }

    [RelayCommand]
    private void ScanPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            PortsListText = ports.Length == 0
                ? "Sin puertos seriales detectados."
                : "Puertos disponibles: " + string.Join(", ", ports);
        }
        catch (Exception ex)
        {
            PortsListText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();

    [RelayCommand]
    private void LoadFileLog()
    {
        var path = _fileLogger.GetCurrentLogFilePath();
        LogFilePath = path;

        if (!File.Exists(path))
        {
            LogFileText = "No hay archivo de log para hoy.";
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
            LogFileText = $"Error leyendo log: {ex.Message}";
            return;
        }

        LogFileText = string.Join("\n", tail);
    }

    [RelayCommand]
    private void EmitReading()
    {
        if (string.IsNullOrEmpty(SelectedSensorId)) return;
        var type = SelectedSensorId.StartsWith("SEN-SPOT-") ? "Ultrasonido" : "LPR";
        _bus.Publish(new SensorReadingReceived(
            SensorId:   SelectedSensorId,
            SensorType: type,
            RawValue:   SelectedStateValue,
            Timestamp:  DateTimeOffset.Now));
        _uiLogger.Log(LogLevel.Info, "Manual", $"Publicado: {SelectedSensorId} = {SelectedStateValue}");
    }

    [RelayCommand]
    private void SimEntryIr()
    {
        _bus.Publish(new SensorReadingReceived("GATE-IR1", "IR", "1", DateTimeOffset.Now));
        _uiLogger.Log(LogLevel.Info, "Manual", "Simulado: GATE-IR1 = 1");
    }

    [RelayCommand]
    private void SimExitIr()
    {
        _bus.Publish(new SensorReadingReceived("GATE-IR2", "IR", "1", DateTimeOffset.Now));
        _uiLogger.Log(LogLevel.Info, "Manual", "Simulado: GATE-IR2 = 1");
    }

    private void AppendLogEntry(LogEntry entry)
    {
        var brush = entry.Level switch
        {
            LogLevel.Error   => AppBrushes.Danger,
            LogLevel.Warning => AppBrushes.Warning,
            LogLevel.Info    => AppBrushes.Tx1,
            _                => AppBrushes.Tx3
        };
        LogLines.Add(new LogLineVm { Text = entry.Formatted, Foreground = brush });
        while (LogLines.Count > 500)
            LogLines.RemoveAt(0);
    }
}
