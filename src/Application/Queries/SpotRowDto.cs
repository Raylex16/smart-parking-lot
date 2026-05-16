namespace SmartParkingLot.Application.Queries;

public record SpotRowDto(
    string Id,
    string Zone,
    string Type,
    string Address,
    bool IsOccupied,
    int Floor
);
