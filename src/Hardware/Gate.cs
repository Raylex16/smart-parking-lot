using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Hardware;

// SOLID - DIP: Implementa IGate definida en Core, permitiendo que Application
// trabaje con la abstracción sin conocer esta implementación concreta.
// GRASP - Polymorphism: Extiende Actuator e implementa IGate para el despacho polimórfico.
public class Gate : Actuator, IGate
{
    private int _angle;
    private GateType _type;

    public Gate(string id, GateType type, int pin)
    {
        _id = id;
        _type = type;
        _pin = pin;
    }

    public void Open()
    {
        _angle = MAX_ANGLE;

        ExecCommand();
        Console.WriteLine($"[Gate {_id}] >>> PUERTA ABIERTA <<<");
    }

    public void Close()
    {
        _angle = MIN_ANGLE;
        ExecCommand();
    }

    public bool GetState()
    {
        // Verdadero si está abierta (ángulo mayor a MIN_ANGLE)
        return _angle > MIN_ANGLE;
    }

    public override void ExecCommand()
    {
        // Aquí se simula el comando o señal física al actuador/pin
        Console.WriteLine($"[Actuator] Ejecutando comando en el PIN {_pin}, Ángulo asignado: {_angle}°");
    }
}
