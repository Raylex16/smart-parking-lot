using System.IO.Ports;
using System.Text;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public class ArduinoSerialBridge : IArduinoReader, ISerialWriter
{
    private const string LogSource = "ArduinoSerialBridge";

    private readonly SerialPort _serialPort;
    private readonly IEventPublisher _events;
    private readonly ILogger _logger;
    private Thread? _readThread;
    private volatile bool _listening;

    private enum CamState { Idle, Receiving }
    private CamState _camState = CamState.Idle;
    private string _camGateId = string.Empty;
    private readonly StringBuilder _camBuffer = new();

    public virtual bool IsListening => _listening;

    public ArduinoSerialBridge(string portName, int baudRate, IEventPublisher events, ILogger logger)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.ReadTimeout = SERIAL_TIMEOUT_MS;
        _serialPort.WriteTimeout = SERIAL_TIMEOUT_MS;
        _events = events;
        _logger = logger;
    }

    public virtual void StartListening()
    {
        try
        {
            if (_serialPort.IsOpen)
                return;

            _serialPort.Open();
            _listening = true;
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "ArduinoSerialBridge-Reader"
            };
            _readThread.Start();
            _logger.Info(LogSource, $"Escuchando en {_serialPort.PortName} a {_serialPort.BaudRate} baud");
        }
        catch (Exception ex)
        {
            _logger.Error(LogSource, $"Error al abrir puerto {_serialPort.PortName}: {ex.ToString()}");
        }
    }

    public virtual void StopListening()
    {
        _listening = false;

        if (_serialPort.IsOpen)
            _serialPort.Close();

        _logger.Info(LogSource, "Escucha detenida");
    }

    private void ReadLoop()
    {
        while (_listening && _serialPort.IsOpen)
        {
            try
            {
                var line = _serialPort.ReadLine().Trim();
                ProcessLine(line);
            }
            catch (TimeoutException)
            {
            }
            catch (Exception ex)
            {
                if (_listening)
                    _logger.Error(LogSource, $"Error de lectura: {ex.Message}");
            }
        }
    }

    private void ProcessLine(string line)
    {
        if (line.StartsWith("CAM:", StringComparison.Ordinal))
        {
            ProcessCameraLine(line);
            return;
        }

        if (SerialProtocol.TryParseEvent(line, out var evt))
        {
            _events.Publish(evt!);
            return;
        }

        var parts = line.Split(':');
        if (parts.Length == 2 && parts[1] is "0" or "1")
        {
            var state = parts[1] == "1" ? "OCUPADO" : "LIBRE";
            _logger.Debug(LogSource, $"{parts[0]} -> {state}");

            _events.Publish(new SensorReadingReceived(parts[0], "SENSOR", parts[1], DateTimeOffset.UtcNow));
            return;
        }

        _logger.Debug(LogSource, $"Linea ignorada: '{line}'");
    }

    private void ProcessCameraLine(string line)
    {
        if (line.StartsWith("CAM:BEGIN:", StringComparison.Ordinal))
        {
            // Format: CAM:BEGIN:{byteCount}:{gateId}
            var parts = line.Split(':', 4);
            if (parts.Length == 4)
            {
                _camState = CamState.Receiving;
                _camGateId = parts[3];
                _camBuffer.Clear();
                _logger.Debug(LogSource, $"CAM frame begin: gate={_camGateId} bytes={parts[2]}");
            }
            return;
        }

        if (line.StartsWith("CAM:DATA:", StringComparison.Ordinal) && _camState == CamState.Receiving)
        {
            _camBuffer.Append(line.AsSpan("CAM:DATA:".Length));
            return;
        }

        if (line == "CAM:END" && _camState == CamState.Receiving)
        {
            try
            {
                var bytes = Convert.FromBase64String(_camBuffer.ToString());
                _events.Publish(new CameraFrameReceived(_camGateId, bytes));
                _logger.Debug(LogSource, $"CAM frame complete: gate={_camGateId} bytes={bytes.Length}");
            }
            catch (FormatException ex)
            {
                _logger.Error(LogSource, $"CAM base64 decode error: {ex.Message}");
                _events.Publish(new CameraFrameReceived(_camGateId, []));
            }
            finally
            {
                _camState = CamState.Idle;
                _camBuffer.Clear();
            }
            return;
        }

        if (line.StartsWith("CAM:ERROR:", StringComparison.Ordinal))
        {
            _logger.Error(LogSource, $"CAM error: {line}");
            _events.Publish(new CameraFrameReceived(_camGateId, []));
            _camState = CamState.Idle;
            _camBuffer.Clear();
        }
    }

    public virtual void WriteLine(string line)
    {
        if (!_serialPort.IsOpen) return;
        _serialPort.WriteLine(line);
    }

    public virtual void Dispose()
    {
        StopListening();
        _serialPort.Dispose();
    }
}
