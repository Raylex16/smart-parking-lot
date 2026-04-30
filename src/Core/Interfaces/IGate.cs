namespace SmartParkingLot.Core.Interfaces;

public interface IGate
{
    void Open();
    void Close();
    bool GetState();
}
