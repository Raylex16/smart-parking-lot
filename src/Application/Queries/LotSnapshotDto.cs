namespace SmartParkingLot.Application.Queries;

public record ZoneSummaryDto(string Zone, int Occupied, int Total);
public record GateSummaryDto(string GateId, string Type, bool IsOpen);
public record LotSnapshotDto(
    Guid Id,
    string Name,
    int TotalSpots,
    int OccupiedSpots,
    IReadOnlyList<ZoneSummaryDto> ZoneSummaries,
    IReadOnlyList<GateSummaryDto> Gates,
    IReadOnlyList<SpotRowDto> Spots
);
