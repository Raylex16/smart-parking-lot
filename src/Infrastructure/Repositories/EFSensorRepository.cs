using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public class EFSensorRepository : ISensorRepository
{
    private readonly ParkingLotDbContext _context;

    public EFSensorRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<bool> LogSensorReadingAsync(string sensorId, string value, DateTime timestamp, 
        CancellationToken ct = default)
    {
        var log = new SensorReadingLog
        {
            SensorId = sensorId,
            Value = value,
            Timestamp = timestamp
        };

        _context.SensorReadingLogs.Add(log);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>>
        GetSensorReadingsAsync(string sensorId, CancellationToken ct = default)
    {
        return await _context.SensorReadingLogs
            .Where(s => s.SensorId == sensorId)
            .OrderByDescending(s => s.Timestamp)
            .Select(s => new ValueTuple<string, string, string, DateTime>(
                s.Id, s.SensorId, s.Value, s.Timestamp))
            .ToListAsync(ct);
    }
}
