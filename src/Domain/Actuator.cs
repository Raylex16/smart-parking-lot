namespace SmartParkingLot.Domain;

public abstract class Actuator
{

    protected string _id = string.Empty;
    protected string _actuatorType = string.Empty; 
    protected int _pin;

    public abstract void ExecCommand();
}