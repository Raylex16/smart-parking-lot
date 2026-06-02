using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public class EFSpotRepository : ISpotRepository
{
    private readonly ParkingLotDbContext _context;

    public EFSpotRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default)
    {
        return await _context.ParkingSpots
            .Where(s => EF.Property<string>(s, "LotId") == lotId && !s.IsOccupied)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default)
    {
        return await _context.ParkingSpots
            .Where(s => EF.Property<string>(s, "LotId") == lotId && s.IsOccupied)
            .ToListAsync(ct);
    }

    public async Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default)
    {
        var spot = await _context.ParkingSpots.FirstOrDefaultAsync(s => s.Id == spotId, ct);
        if (spot is null) return false;

        spot.ApplyOccupancy(isOccupied, "repository");
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, 
        string floor, CancellationToken ct = default)
    {
        var exists = await _context.ParkingSpots.AnyAsync(s => s.Id == spotId, ct);
        if (exists) return;

        var spot = new ParkingSpot(spotId, address, type, floor);
        _context.ParkingSpots.Add(spot);
        _context.Entry(spot).Property("LotId").CurrentValue = lotId;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> RemoveOrphanSpotsAsync(string lotId, IEnumerable<string> validSpotIds, 
        CancellationToken ct = default)
    {
        var validIds = validSpotIds.ToList();
        var orphanSpots = await _context.ParkingSpots
            .Where(s => EF.Property<string>(s, "LotId") == lotId && !validIds.Contains(s.Id))
            .ToListAsync(ct);

        _context.ParkingSpots.RemoveRange(orphanSpots);
        await _context.SaveChangesAsync(ct);
        return orphanSpots.Count;
    }
}
