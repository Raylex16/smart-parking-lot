using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Application.Monitoring;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Application.Sensors;
using SmartParkingLot.Core.Interfaces;  // IHardwareStatus, LogLevel
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Infrastructure;
using SmartParkingLot.Gui.Resources;

namespace SmartParkingLot.Gui.ViewModels;

public partial class HardwarePageViewModel : ObservableObject
{
    private const int FILE_LOG_TAIL_LINES = 80;

    private readonly IHardwareStatus _hardwareStatus;
    private readonly IArduinoMonitoringService _monitoring;
    private readonly IHardwareConfigurationService _hwConfig;
    private readonly ILogQueryService _logQuery;
    private readonly IManualSensorService _manualSensor;
    private readonly GuiLogger _uiLogger;
    private readonly IUiThreadDispatcher _ui;

    private Action<LogEntry>? _logHandler;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionLabel = "Desconectado";
    [ObservableProperty] private string _connectButtonGlyph = "";
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
        IArduinoMonitoringService monitoring,
        IHardwareConfigurationService hwConfig,
        ILogQueryService logQuery,
        IManualSensorService manualSensor,
        GuiLogger uiLogger,
        IUiThreadDispatcher ui)
    {
        _hardwareStatus = hardwareStatus;
        _monitoring     = monitoring;
        _hwConfig       = hwConfig;
        _logQuery       = logQuery;
        _manualSensor   = manualSensor;
        _uiLogger       = uiLogger;
        _ui             = ui;
    }

    public void Activate()
    {
        var snapshot = _hwConfig.GetSnapshot();
        ConfigPort     = snapshot.Port;
        ConfigBaudRate = snapshot.BaudRate.ToString();

        SensorIds.Clear();
        foreach (var id in _manualSensor.SensorIds)
            SensorIds.Add(id);
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
        IsConnected        = _monitoring.IsRunning;
        ConnectionLabel    = _monitoring.IsRunning ? "Conectado" : "Desconectado";
        ConnectButtonGlyph = _monitoring.IsRunning ? "" : "";
        ConnectButtonLabel = _monitoring.IsRunning ? "Desconectar" : "Conectar";
    }

    [RelayCommand]
    private void ToggleConnection()
    {
        try
        {
            if (_monitoring.IsRunning)
                _monitoring.Stop();
            else
                _monitoring.Start();
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
        var available = new AvailableSerialPortsQuery().ListPorts();
        PortsListText = available.Count == 0
            ? "Sin puertos seriales detectados."
            : "Puertos disponibles: " + string.Join(", ", available);
    }

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();

    [RelayCommand]
    private void LoadFileLog()
    {
        var path = _logQuery.GetCurrentLogFilePath();
        LogFilePath = path;

        var tail = _logQuery.TailLogFile(FILE_LOG_TAIL_LINES);
        if (tail.Count == 0)
        {
            LogFileText = "No hay archivo de log para hoy.";
            return;
        }

        LogFileText = string.Join("\n", tail);
    }

    [RelayCommand]
    private async Task EmitReading()
    {
        if (string.IsNullOrEmpty(SelectedSensorId)) return;
        var spotId = SelectedSensorId.StartsWith("SEN-SPOT-")
            ? SelectedSensorId.Replace("SEN-SPOT-", "")
            : null;

        if (spotId is not null)
        {
            var occupied = SelectedStateValue == "1";
            await _manualSensor.RecordSpotReadingAsync(spotId, occupied);
        }
        _uiLogger.Log(LogLevel.Info, "Manual", $"Publicado: {SelectedSensorId} = {SelectedStateValue}");
    }

    [RelayCommand]
    private async Task SimEntryIr()
    {
        await _manualSensor.TriggerGateIrAsync(GuiConstants.ENTRY_GATE_ID);
        _uiLogger.Log(LogLevel.Info, "Manual", "Simulado: GATE-IR1 = 1");
    }

    [RelayCommand]
    private async Task SimExitIr()
    {
        await _manualSensor.TriggerGateIrAsync(GuiConstants.EXIT_GATE_ID);
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
