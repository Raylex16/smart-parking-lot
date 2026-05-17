namespace SmartParkingLot.Application.Hardware;

public interface IAvailableSerialPortsQuery
{
    IReadOnlyList<string> ListPorts();
}
