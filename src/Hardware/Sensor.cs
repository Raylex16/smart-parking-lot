using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public class Sensor<T> : ISensor, ISensorCapture<T> where T : SensorReading
{
    private readonly string _id;
    private readonly string _type;
    private readonly ILogger _logger;
    private T? _snapshot;

    public string Id => _id;

    public Sensor(string id, string type, ILogger logger)
    {
        _id = id;
        _type = type;
        _logger = logger;
    }

    public float ReadValue()
    {
        if (_snapshot is null)
        {
            _logger.Debug($"Sensor {_id}", "Sin lecturas registradas, retornando 0");
            return 0f;
        }

        _logger.Debug($"Sensor {_id}", $"Leyendo valor: {_snapshot.RegisteredValue}");
        return _snapshot.RegisteredValue;
    }

    public string GetSensorType() => _type;

    public T CaptureReading(T reading)
    {
        _logger.Info($"Sensor {_id}", $"Captura registrada — {reading}");
        _snapshot = reading;
        return reading;
    }

    public T? GetSnapshot() => _snapshot;

    public override string ToString() =>
        $"[Sensor {_id} | Tipo: {_type}]";
}
