namespace SmartParkingLot.Core.Interfaces;

public interface IDisplay
{
    void ShowCapacity(int available, int total);
    void ShowMessage(string text);
}
