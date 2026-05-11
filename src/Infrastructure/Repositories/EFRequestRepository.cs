using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public class EFRequestRepository : IRequestRepository
{
    private readonly ParkingLotDbContext _context;

    public EFRequestRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType, 
        string lotId, DateTime timestamp, bool approved, string? releasedSpotId = null, 
        CancellationToken ct = default)
    {
        var log = new RequestLog
        {
            RequestId = requestId,
            VehiclePlate = vehiclePlate,
            RequestType = requestType,
            LotId = lotId,
            Timestamp = timestamp,
            Approved = approved,
            ReleasedSpotId = releasedSpotId
        };

        _context.RequestLogs.Add(log);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>>
        GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default)
    {
        return await _context.RequestLogs
            .Where(r => r.VehiclePlate == vehiclePlate)
            .OrderByDescending(r => r.Timestamp)
            .Select(r => new ValueTuple<string, string, string, DateTime, bool>(
                r.RequestId, r.VehiclePlate, r.RequestType, r.Timestamp, r.Approved))
            .ToListAsync(ct);
    }
}
