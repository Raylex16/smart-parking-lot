using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

// GRASP - Polymorphism + Information Expert: Sensor genérico que captura lecturas tipadas
// La simulación de hardware se hace en CaptureReading(), similar a Gate.ExecCommand()
public class Sensor<T> : ISensor, ISensorCapture<T> where T : SensorReading
{
    private readonly string _id;
    private readonly string _type;
    private T? _snapshot;

    public string Id => _id;

    public Sensor(string id, string type)
    {
        _id = id;
        _type = type;
    }

    public float ReadValue()
    {
        // Simulación: retorna el valor de la última lectura capturada
        if (_snapshot is null)
        {
            Console.WriteLine($"[Sensor {_id}] Sin lecturas registradas, retornando 0");
            return 0f;
        }

        Console.WriteLine($"[Sensor {_id}] Leyendo valor: {_snapshot.RegisteredValue}");
        return _snapshot.RegisteredValue;
    }

    public string GetSensorType() => _type;

    public T CaptureReading(T reading)
    {
        // Simulación del hardware: en un sistema real aquí se leería el pin físico
        Console.WriteLine($"[Sensor {_id}] Captura registrada — {reading}");
        _snapshot = reading;
        return reading;
    }

    public T? GetSnapshot() => _snapshot;

    public override string ToString() =>
        $"[Sensor {_id} | Tipo: {_type}]";
}
