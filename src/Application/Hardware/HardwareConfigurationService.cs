using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Application.Hardware;

public sealed class HardwareConfigurationService : IHardwareConfigurationService
{
    private readonly HardwareConfig _config;
    private readonly IHardwareStatus _status;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly IReadOnlyDictionary<string, Sensor<SpotSensorReading>> _spotSensors;

    public HardwareConfigurationService(
        HardwareConfig config,
        IHardwareStatus status,
        Sensor<GateSensorReading> gateSensor,
        IReadOnlyDictionary<string, Sensor<SpotSensorReading>> spotSensors)
    {
        _config      = config;
        _status      = status;
        _gateSensor  = gateSensor;
        _spotSensors = spotSensors;
    }

    public HardwareSnapshotDto GetSnapshot()
    {
        var sensors = _config.Sensors
            .Select(s => new SensorInfoDto(s.SensorId, s.SpotId, s.ActuatorId, s.Address, s.Type, s.Floor))
            .ToList()
            .AsReadOnly();

        var gates = _config.Gates
            .Select(g => new HardwareGateInfoDto(g.GateId, g.IrSensorId, g.ActuatorId, g.Pin))
            .ToList()
            .AsReadOnly();

        var spotSensorIds = _spotSensors.Values.Select(s => s.Id).ToList().AsReadOnly();

        return new HardwareSnapshotDto(
            Port:          _config.Port,
            BaudRate:      _config.BaudRate,
            IsConnected:   _status.IsConnected,
            Sensors:       sensors,
            Gates:         gates,
            SpotSensorIds: spotSensorIds,
            GateSensorId:  _gateSensor.Id);
    }

    public IReadOnlyList<string> GetAllSensorIds()
    {
        var ids = new List<string> { _gateSensor.Id };
        ids.AddRange(_spotSensors.Values.Select(s => s.Id));
        return ids.AsReadOnly();
    }
}
