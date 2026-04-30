
using System.IO.Ports;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public class ArduinoSerialBridge : IArduinoReader
{
    private readonly SerialPort _serialPort;
    private readonly IEventPublisher _events;
    private Thread? _readThread;
    private volatile bool _listening;
    private volatile bool _consoleLoggingEnabled;

    public bool IsListening => _listening;
    public bool ConsoleLoggingEnabled
    {
        get => _consoleLoggingEnabled;
        set => _consoleLoggingEnabled = value;
    }

    public ArduinoSerialBridge(string portName, int baudRate, IEventPublisher events)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.ReadTimeout = SERIAL_TIMEOUT_MS;
        _serialPort.WriteTimeout = SERIAL_TIMEOUT_MS;
        _events = events;
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
            if (ConsoleLoggingEnabled)
                Console.WriteLine($"[ArduinoSerialBridge] Escuchando en {_serialPort.PortName} a {_serialPort.BaudRate} baud");
        }
        catch (Exception ex)
        {
            if (ConsoleLoggingEnabled)
                Console.WriteLine($"[ArduinoSerialBridge] Error al abrir puerto {_serialPort.PortName}: {ex.Message}");
        }
    }

    public void StopListening()
    {
        _listening = false;

        if (_serialPort.IsOpen)
            _serialPort.Close();

        if (ConsoleLoggingEnabled)
            Console.WriteLine("[ArduinoSerialBridge] Escucha detenida");
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
                if (_listening && ConsoleLoggingEnabled)
                    Console.WriteLine($"[ArduinoSerialBridge] Error de lectura: {ex.Message}");
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
            if (ConsoleLoggingEnabled)
            {
                var state = parts[1] == "1" ? "OCUPADO" : "LIBRE";
                Console.WriteLine($"[ArduinoSerialBridge] {parts[0]} -> {state}");
            }

            _events.Publish(new SensorReadingReceived(parts[0], "SENSOR", parts[1], DateTimeOffset.UtcNow));
            return;
        }

        if (ConsoleLoggingEnabled)
            Console.WriteLine($"[ArduinoSerialBridge] Linea ignorada: '{line}'");
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
