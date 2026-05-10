using SmartParkingLot.Core;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public class Gate : Actuator, IGate
{
    private readonly ILogger _logger;
    private readonly ICommandDispatcher _dispatcher;
    private readonly string _actuatorId;
    private int _angle;
    private GateType _type;

    public Gate(string id, GateType type, int pin, string actuatorId, ICommandDispatcher dispatcher, ILogger logger)
    {
        _id = id;
        _type = type;
        _pin = pin;
        _actuatorId = actuatorId;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public void Open()
    {
        _angle = MAX_ANGLE;
        DispatchAngle();
        _logger.Info($"Gate {_id}", ">>> PUERTA ABIERTA <<<");

        _ = Task.Delay(GATE_AUTO_CLOSE_MS).ContinueWith(_ => SafeClose());
    }

    public void Close()
    {
        _angle = MIN_ANGLE;
        DispatchAngle();
        _logger.Info($"Gate {_id}", "<<< PUERTA CERRADA >>>");
    }

    public bool GetState()
    {
        return _angle > MIN_ANGLE;
    }

    public override void ExecCommand()
    {
        DispatchAngle();
    }

    private void DispatchAngle()
    {
        var commandId = Guid.NewGuid().ToString("N")[..8];
        _dispatcher.Dispatch(new ActuatorCommand(commandId, _actuatorId, "ANGLE", _angle.ToString()));
        _logger.Debug($"Gate {_id}", $"-> {_actuatorId} ANGLE:{_angle} (PIN {_pin})");
    }

    private void SafeClose()
    {
        try
        {
            Close();
        }
        catch (Exception ex)
        {
            _logger.Error($"Gate {_id}", $"Error en auto-cierre: {ex.Message}");
        }
    }
}
