using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Handlers;

public sealed class SpotOccupancyChangedHandler
{
    private readonly ICommandDispatcher _dispatcher;
    private readonly IReadOnlyDictionary<string, string> _spotToActuator;

    public SpotOccupancyChangedHandler(
        ICommandDispatcher dispatcher,
        IReadOnlyDictionary<string, string> spotToActuator)
    {
        _dispatcher = dispatcher;
        _spotToActuator = spotToActuator;
    }

    public void Handle(SpotOccupancyChanged evt)
    {
        if (!_spotToActuator.TryGetValue(evt.SpotId, out var actuatorId)) return;
        _dispatcher.Dispatch(new ActuatorCommand(
            CommandId: Guid.NewGuid().ToString("N")[..8],
            ActuatorId: actuatorId,
            Action: "SET",
            Payload: evt.IsOccupied ? "1" : "0"));
    }
}
