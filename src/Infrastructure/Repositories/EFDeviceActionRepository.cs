using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public class EFDeviceActionRepository : IDeviceActionRepository
{
    private readonly ParkingLotDbContext _context;

    public EFDeviceActionRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<bool> LogDeviceActionAsync(string deviceId, string action, DateTime timestamp, 
        CancellationToken ct = default)
    {
        var log = new DeviceActionLog
        {
            DeviceId = deviceId,
            Action = action,
            Timestamp = timestamp
        };

        _context.DeviceActionLogs.Add(log);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>>
        GetDeviceActionsAsync(string deviceId, CancellationToken ct = default)
    {
        return await _context.DeviceActionLogs
            .Where(d => d.DeviceId == deviceId)
            .OrderByDescending(d => d.Timestamp)
            .Select(d => new ValueTuple<string, string, string, DateTime>(
                d.Id, d.DeviceId, d.Action, d.Timestamp))
            .ToListAsync(ct);
    }
}
