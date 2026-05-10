using System.IO.Ports;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public class ArduinoSerialBridge : IArduinoReader
{
    private const string LogSource = "ArduinoSerialBridge";

    private readonly SerialPort _serialPort;
    private readonly IEventPublisher _events;
    private readonly ILogger _logger;
    private Thread? _readThread;
    private volatile bool _listening;

    public bool IsListening => _listening;

    public ArduinoSerialBridge(string portName, int baudRate, IEventPublisher events, ILogger logger)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.ReadTimeout = SERIAL_TIMEOUT_MS;
        _serialPort.WriteTimeout = SERIAL_TIMEOUT_MS;
        _events = events;
        _logger = logger;
    }

    public void StartListening()
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

    public void StopListening()
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

    public void WriteLine(string line)
    {
        if (!_serialPort.IsOpen) return;
        _serialPort.WriteLine(line);
    }

    public void Dispose()
    {
        StopListening();
        _serialPort.Dispose();
    }
}
