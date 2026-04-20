
using System.IO.Ports;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

// GRASP - Pure Fabrication: No existe en el dominio del parqueadero.
// Es infraestructura que traduce comunicacion serial a eventos de dominio.
// SOLID - DIP: Depende de IEventPublisher, no de sensores concretos.
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

    /// <summary>
    /// Crea un bridge serial hacia Arduino.
    /// </summary>
    /// <param name="portName">Puerto serial (ej. "COM6")</param>
    /// <param name="baudRate">Velocidad en baudios (ej. 9600)</param>
    /// <param name="events">Bus de eventos para publicar lecturas inbound.</param>
    public ArduinoSerialBridge(string portName, int baudRate, IEventPublisher events)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.ReadTimeout = SERIAL_TIMEOUT_MS;
        _serialPort.WriteTimeout = SERIAL_TIMEOUT_MS;
        _events = events;
    }

    /// <summary>
    /// Abre el puerto serial e inicia un hilo de fondo para lectura continua.
    /// </summary>
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

    /// <summary>
    /// Detiene la lectura y cierra el puerto serial.
    /// </summary>
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
                // Sin datos en este ciclo, continuar
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
        // Formato nuevo: EVT:SENSOR:<id>:<value>
        if (SerialProtocol.TryParseEvent(line, out var evt))
        {
            _events.Publish(evt!);
            return;
        }

        // Compat V0: <id>:<value>  (ej. "IR1:1")
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
