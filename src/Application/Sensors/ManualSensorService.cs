using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

namespace SmartParkingLot.Application.Sensors;

public sealed class ManualSensorService : IManualSensorService
{
    private readonly IReadOnlyDictionary<string, Sensor<SpotSensorReading>> _spotSensors;
    private readonly Sensor<GateSensorReading> _gateSensor;
    private readonly IEventPublisher _bus;
    private readonly IParkingRepository _repository;
    private readonly IReadOnlyDictionary<string, string> _gateToIrSensor;

    public IReadOnlyList<string> SensorIds { get; }

    public ManualSensorService(
        IReadOnlyDictionary<string, Sensor<SpotSensorReading>> spotSensors,
        Sensor<GateSensorReading> gateSensor,
        IEventPublisher bus,
        IParkingRepository repository,
        IReadOnlyDictionary<string, string> gateToIrSensor)
    {
        _spotSensors    = spotSensors;
        _gateSensor     = gateSensor;
        _bus            = bus;
        _repository     = repository;
        _gateToIrSensor = gateToIrSensor;

        var ids = new List<string> { gateSensor.Id };
        ids.AddRange(spotSensors.Values.Select(s => s.Id));
        SensorIds = ids.AsReadOnly();
    }

    public async Task RecordSpotReadingAsync(string spotId, bool occupied, CancellationToken ct = default)
    {
        if (!_spotSensors.TryGetValue(spotId, out var sensor))
            throw new InvalidOperationException($"No hay sensor registrado para el espacio '{spotId}'.");

        var reading = new SpotSensorReading(spotId, occupied);
        sensor.CaptureReading(reading);

        var rawValue = occupied ? "1" : "0";
        await _repository.LogSensorReadingAsync(sensor.Id, rawValue, DateTime.Now, ct);

        _bus.Publish(new SensorReadingReceived(
            SensorId:   sensor.Id,
            SensorType: sensor.GetSensorType(),
            RawValue:   rawValue,
            Timestamp:  DateTimeOffset.Now));
    }

    public Task TriggerGateIrAsync(string gateId, CancellationToken ct = default)
    {
        if (!_gateToIrSensor.TryGetValue(gateId, out var irSensorId))
            throw new InvalidOperationException($"No hay sensor IR registrado para la puerta '{gateId}'.");

        _bus.Publish(new SensorReadingReceived(
            SensorId:   irSensorId,
            SensorType: "IR",
            RawValue:   "1",
            Timestamp:  DateTimeOffset.Now));

        return Task.CompletedTask;
    }
}
