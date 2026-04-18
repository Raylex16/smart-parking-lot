
using System.IO.Ports;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Hardware;

// GRASP - Pure Fabrication: No existe en el dominio del parqueadero.
// Es infraestructura que traduce comunicacion serial a lecturas de dominio.
// SOLID - DIP: Depende de ISensorCapture<SpotSensorReading>, no del concreto Sensor<T>.
public class ArduinoSerialBridge : IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)> _sensorMap;
    private Thread? _readThread;
    private volatile bool _listening;

    /// <summary>
    /// Crea un bridge serial hacia Arduino.
    /// </summary>
    /// <param name="portName">Puerto serial (ej. "COM6")</param>
    /// <param name="baudRate">Velocidad en baudios (ej. 9600)</param>
    /// <param name="sensorMap">Mapeo: hardware ID -> (spot ID del dominio, sensor). El bridge traduce IDs de hardware a IDs de dominio.</param>
    public ArduinoSerialBridge(
        string portName,
        int baudRate,
        Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)> sensorMap)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.ReadTimeout = SERIAL_TIMEOUT_MS;
        _serialPort.WriteTimeout = SERIAL_TIMEOUT_MS;
        _sensorMap = sensorMap;
    }

    /// <summary>
    /// Abre el puerto serial e inicia un hilo de fondo para lectura continua.
    /// </summary>
    public void StartListening()
    {
        try
        {
            _serialPort.Open();
            _listening = true;
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "ArduinoSerialBridge-Reader"
            };
            _readThread.Start();
            Console.WriteLine($"[ArduinoSerialBridge] Escuchando en {_serialPort.PortName} a {_serialPort.BaudRate} baud");
        }
        catch (Exception ex)
        {
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
                if (_listening)
                    Console.WriteLine($"[ArduinoSerialBridge] Error de lectura: {ex.Message}");
            }
        }
    }

    private void ProcessLine(string line)
    {
        // Formato esperado: SENSOR_ID:VALOR (ej. "IR1:1")
        var parts = line.Split(':');

        if (parts.Length != 2)
        {
            Console.WriteLine($"[ArduinoSerialBridge] Formato invalido: '{line}'");
            return;
        }

        var sensorId = parts[0];
        var rawValue = parts[1];

        if (rawValue is not ("0" or "1"))
        {
            Console.WriteLine($"[ArduinoSerialBridge] Valor invalido para '{sensorId}': '{rawValue}' (esperado 0 o 1)");
            return;
        }

        if (!_sensorMap.TryGetValue(sensorId, out var entry))
        {
            Console.WriteLine($"[ArduinoSerialBridge] Sensor no mapeado: '{sensorId}'");
            return;
        }

        var isOccupied = rawValue == "1";
        // Se usa el spotId del dominio (ej. "A1"), no el hardware ID (ej. "IR1")
        var reading = new SpotSensorReading(entry.SpotId, isOccupied);
        entry.Sensor.CaptureReading(reading);

        Console.WriteLine($"[ArduinoSerialBridge] {sensorId} ({entry.SpotId}) -> {(isOccupied ? "OCUPADO" : "LIBRE")}");
    }

    public void Dispose()
    {
        StopListening();
        _serialPort.Dispose();
    }
}
