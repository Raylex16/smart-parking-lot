namespace SmartParkingLot.Core.Interfaces;

public interface ISensor
{
    float ReadValue();
    string GetSensorType();
}
