using System.Collections.Concurrent;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public sealed class SerialCommandDispatcher : ICommandDispatcher, IDisposable
{
    private const string LogSource = "SerialCommandDispatcher";

    private readonly ArduinoSerialBridge _bridge;
    private readonly ILogger _logger;
    private readonly BlockingCollection<ActuatorCommand> _queue = new();
    private readonly Thread _writer;
    private volatile bool _running = true;

    public SerialCommandDispatcher(ArduinoSerialBridge bridge, ILogger logger)
    {
        _bridge = bridge;
        _logger = logger;
        _writer = new Thread(Loop) { IsBackground = true, Name = "SerialCommandDispatcher-Writer" };
        _writer.Start();
    }

    public void Dispatch(ActuatorCommand command) => _queue.Add(command);

    private void Loop()
    {
        foreach (var cmd in _queue.GetConsumingEnumerable())
        {
            if (!_running) break;
            try
            {
                _bridge.WriteLine(SerialProtocol.SerializeCommand(cmd));
                _logger.Debug(LogSource, $"-> {cmd.CommandId} {cmd.ActuatorId} {cmd.Action}:{cmd.Payload}");
            }
            catch (Exception ex)
            {
                _logger.Error(LogSource, $"Error enviando {cmd.CommandId}: {ex.ToString()}");
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _queue.CompleteAdding();
    }
}
