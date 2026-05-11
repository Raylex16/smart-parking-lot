using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public class EFParkingLotRepository : IParkingLotRepository
{
    private readonly ParkingLotDbContext _context;

    public EFParkingLotRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots
            .FirstOrDefaultAsync(l => l.Id == lotId, ct);

        if (lot is null) return null;

        var spots = await _context.ParkingSpots
            .Where(s => EF.Property<string>(s, "LotId") == lotId)
            .ToListAsync(ct);

        foreach (var spot in spots)
            lot.AddSpot(spot);

        return lot;
    }

    public async Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default)
    {
        return await _context.ParkingSpots
            .Where(s => EF.Property<string>(s, "LotId") == lotId)
            .ToListAsync(ct);
    }
}
