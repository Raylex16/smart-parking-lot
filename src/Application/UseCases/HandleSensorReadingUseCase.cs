using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;

namespace SmartParkingLot.Application.UseCases;

public sealed class HandleSensorReadingUseCase
{
    private readonly ParkingLot _lot;
    private readonly IReadOnlyDictionary<string, string> _sensorToSpot;

    public HandleSensorReadingUseCase(ParkingLot lot, IReadOnlyDictionary<string, string> sensorToSpot)
    {
        _lot = lot;
        _sensorToSpot = sensorToSpot;
    }

    public void Handle(SensorReadingReceived evt)
    {
        if (!_sensorToSpot.TryGetValue(evt.SensorId, out var spotId)) return;
        if (evt.RawValue is not ("0" or "1")) return;
        var spot = _lot.GetSpots().FirstOrDefault(s => s.Id == spotId);
        spot?.ApplyOccupancy(evt.RawValue == "1", $"sensor:{evt.SensorId}");
    }
}
