namespace SmartParkingLot.Hardware;

// GRASP - Polymorphism: Clase abstracta base para todos los actuadores IoT (puertas, luces, etc.)
// GRASP - Pure Fabrication: Abstracción que no existe en el dominio real del parqueadero,
// sino que modela el concepto de hardware que ejecuta comandos físicos.
public abstract class Actuator
{
    protected string _id = string.Empty;
    protected string _actuatorType = string.Empty;
    protected int _pin;

    public abstract void ExecCommand();
}
