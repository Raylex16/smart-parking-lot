namespace SmartParkingLot.Domain;

// GRASP - Polymorphism: Interfaz que abstrae cualquier tipo de sensor IoT
public interface ISensor
{
    float ReadValue();
    string GetSensorType();
}
