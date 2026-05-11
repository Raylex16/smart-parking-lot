using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Display;

public sealed class LcdDisplay : IDisplay
{
    private readonly ICommandDispatcher _dispatcher;

    public LcdDisplay(ICommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void ShowCapacity(int available, int total)
    {
        _dispatcher.Dispatch(new ActuatorCommand(
            CommandId: NewId(),
            ActuatorId: LCD_ACTUATOR_ID,
            Action: "STATUS",
            Payload: $"{available}:{total}"));
    }

    public void ShowMessage(string text)
    {
        _dispatcher.Dispatch(new ActuatorCommand(
            CommandId: NewId(),
            ActuatorId: LCD_ACTUATOR_ID,
            Action: "MSG",
            Payload: text));
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
