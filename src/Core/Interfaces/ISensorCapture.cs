using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Ports;

// SOLID - ISP: Separa la capacidad de capturar lecturas de la capacidad de leerlas (ISensor)
// SOLID - DIP: Permite que consumidores dependan de esta abstraccion, no del concreto Sensor<T>
public interface ISensorCapture<T> where T : SensorReading
{
    T CaptureReading(T reading);
}
