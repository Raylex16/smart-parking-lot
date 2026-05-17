namespace SmartParkingLot.Application.Hardware;

public interface IHardwareConfigurationService
{
    HardwareSnapshotDto GetSnapshot();
    IReadOnlyList<string> GetAllSensorIds();
}
