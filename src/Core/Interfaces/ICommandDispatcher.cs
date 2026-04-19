using SmartParkingLot.Core.Commands;

namespace SmartParkingLot.Core.Interfaces;

public interface ICommandDispatcher
{
    void Dispatch(ActuatorCommand command);
}
