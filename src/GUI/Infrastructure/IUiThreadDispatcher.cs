namespace SmartParkingLot.Gui.Infrastructure;

public interface IUiThreadDispatcher
{
    void Enqueue(Action action);
}
