using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

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
        return _angle > MIN_ANGLE;
    }

    public override void ExecCommand()
    {
        Console.WriteLine($"[Actuator] Ejecutando comando en el PIN {_pin}, Ángulo asignado: {_angle}°");
    }
}
