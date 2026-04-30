using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

public interface ISensorCapture<T> where T : SensorReading
{
    T CaptureReading(T reading);
}
