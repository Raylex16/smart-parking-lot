using System;
using System.Collections.Generic;
using System.Text;




namespace SmartParkingLot.Domain;



public class Gate : Actuator
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
        _angle = MaxAngle;

        ExecCommand();
        Console.WriteLine($"[Gate {_id}] >>> PUERTA ABIERTA <<<");
    }

    public void Close()
    {
        _angle = MinAngle;
        ExecCommand();
    }

    public bool GetState()
    {
        // Verdadero si está abierta (ángulo mayor a MinAngle)
        return _angle > MinAngle; 
    }

    public override void ExecCommand()
    {
        // Aquí se simula el comando o señal física al actuador/pin
        System.Console.WriteLine($"[Actuator] Ejecutando comando en el PIN {_pin}, Ángulo asignado: {_angle}°");
    }
}
