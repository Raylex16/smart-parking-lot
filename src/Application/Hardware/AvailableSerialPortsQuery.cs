using System.IO.Ports;

namespace SmartParkingLot.Application.Hardware;

public sealed class AvailableSerialPortsQuery : IAvailableSerialPortsQuery
{
    public IReadOnlyList<string> ListPorts()
    {
        try
        {
            return SerialPort.GetPortNames().OrderBy(p => p).ToList().AsReadOnly();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
