using System.Collections.Concurrent;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public sealed class SerialCommandDispatcher : ICommandDispatcher, IDisposable
{
    private readonly ArduinoSerialBridge _bridge;
    private readonly BlockingCollection<ActuatorCommand> _queue = new();
    private readonly Thread _writer;
    private volatile bool _running = true;

    public SerialCommandDispatcher(ArduinoSerialBridge bridge)
    {
        _bridge = bridge;
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
                Console.WriteLine($"[Dispatcher] -> {cmd.CommandId} {cmd.ActuatorId} {cmd.Action}:{cmd.Payload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dispatcher] Error enviando {cmd.CommandId}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _queue.CompleteAdding();
    }
}
