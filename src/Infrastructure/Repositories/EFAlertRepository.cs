using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public class EFAlertRepository : IAlertRepository
{
    private readonly ParkingLotDbContext _context;

    public EFAlertRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<bool> LogAlertAsync(string alertId, string type, string message, DateTime timestamp, 
        CancellationToken ct = default)
    {
        var log = new AlertLog
        {
            Type = type,
            Message = message,
            Timestamp = timestamp
        };

        _context.AlertLogs.Add(log);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
