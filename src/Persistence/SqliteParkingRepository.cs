using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Persistence;

public class SqliteParkingRepository : IParkingRepository
{
    private readonly string _connectionString;

    public SqliteParkingRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = "SELECT Id, Name, Mode FROM ParkingLots WHERE Id = @LotId;";
        var lotDto = await connection.QueryFirstOrDefaultAsync<ParkingLotDto>(
            new CommandDefinition(sql, new { LotId = lotId }, cancellationToken: ct));

        if (lotDto is null) return null;

        var spotsSql = "SELECT Id, Address, Type, Floor, IsOccupied FROM ParkingSpots WHERE LotId = @LotId;";
        var spotsDto = await connection.QueryAsync<ParkingSpotDto>(
            new CommandDefinition(spotsSql, new { LotId = lotId }, cancellationToken: ct));

        var lot = new ParkingLot(lotDto.Id, lotDto.Name, Enum.Parse<ParkingMode>(lotDto.Mode));

        foreach (var spotDto in spotsDto)
        {
            var spot = new ParkingSpot(spotDto.Id, spotDto.Address, spotDto.Type, spotDto.Floor);
            if (spotDto.IsOccupied == 1)
            {
                try { spot.Occupy(); } catch { }
            }
            lot.AddSpot(spot);
        }

        return lot;
    }

    public async Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = "SELECT Id, Address, Type, Floor, IsOccupied FROM ParkingSpots WHERE LotId = @LotId;";
        var spotsDto = await connection.QueryAsync<ParkingSpotDto>(
            new CommandDefinition(sql, new { LotId = lotId }, cancellationToken: ct));

        var spots = new List<ParkingSpot>();

        foreach (var spotDto in spotsDto)
        {
            var spot = new ParkingSpot(spotDto.Id, spotDto.Address, spotDto.Type, spotDto.Floor);
            if (spotDto.IsOccupied == 1)
            {
                try { spot.Occupy(); } catch { }
            }
            spots.Add(spot);
        }

        return spots;
    }

    public Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public async Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, string floor,
        CancellationToken ct = default)
    {
        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT OR IGNORE INTO ParkingSpots (Id, LotId, Address, Type, Floor, IsOccupied)
            VALUES (@Id, @LotId, @Address, @Type, @Floor, 0);
        """;
        await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new { Id = spotId, LotId = lotId, Address = address, Type = type, Floor = floor },
                cancellationToken: ct));
    }

    public async Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            UPDATE ParkingSpots
            SET IsOccupied = @IsOccupied
            WHERE Id = @SpotId;
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new { SpotId = spotId, IsOccupied = isOccupied ? 1 : 0 },
                cancellationToken: ct));

        return result > 0;
    }

    public async Task<bool> DeleteParkingSpotAsync(string spotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = "DELETE FROM ParkingSpots WHERE Id = @SpotId;";
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { SpotId = spotId }, cancellationToken: ct));

        return result > 0;
    }

    public async Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType,
        string lotId, DateTime timestamp, bool approved, string? releasedSpotId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(vehiclePlate);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT INTO RequestLogs (Id, VehiclePlate, RequestType, LotId, ReleasedSpotId, Timestamp, Approved)
            VALUES (@Id, @VehiclePlate, @RequestType, @LotId, @ReleasedSpotId, @Timestamp, @Approved);
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new
                {
                    Id = requestId,
                    VehiclePlate = vehiclePlate,
                    RequestType = requestType,
                    LotId = lotId,
                    ReleasedSpotId = releasedSpotId,
                    Timestamp = timestamp,
                    Approved = approved ? 1 : 0
                },
                cancellationToken: ct));

        return result > 0;
    }

    public async Task<IEnumerable<(string RequestId, string VehiclePlate, string RequestType, DateTime Timestamp, bool Approved)>>
        GetRequestHistoryAsync(string vehiclePlate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiclePlate);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, VehiclePlate, RequestType, Timestamp, Approved
            FROM RequestLogs
            WHERE VehiclePlate = @VehiclePlate
            ORDER BY Timestamp DESC;
        """;
        var records = await connection.QueryAsync<RequestLogDto>(
            new CommandDefinition(sql, new { VehiclePlate = vehiclePlate }, cancellationToken: ct));

        return records.Select(r =>
            (r.Id, r.VehiclePlate, r.RequestType, r.Timestamp, r.Approved == 1)
        ).ToList();
    }

    public async Task<bool> LogSensorReadingAsync(string sensorId, string value,
        DateTime timestamp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sensorId);
        ArgumentNullException.ThrowIfNull(value);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT INTO SensorReadings (Id, SensorId, Value, Timestamp)
            VALUES (@Id, @SensorId, @Value, @Timestamp);
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new
                {
                    Id = $"SR-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    SensorId = sensorId,
                    Value = value,
                    Timestamp = timestamp
                },
                cancellationToken: ct));

        return result > 0;
    }

    public async Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>>
        GetSensorReadingsAsync(string sensorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sensorId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, SensorId, Value, Timestamp
            FROM SensorReadings
            WHERE SensorId = @SensorId
            ORDER BY Timestamp DESC;
        """;
        var records = await connection.QueryAsync<(string Id, string SensorId, string Value, DateTime Timestamp)>(
            new CommandDefinition(sql, new { SensorId = sensorId }, cancellationToken: ct));

        return records;
    }

    public async Task<bool> LogDeviceActionAsync(string deviceId, string action,
        DateTime timestamp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(action);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT INTO DeviceActions (Id, DeviceId, Action, Timestamp)
            VALUES (@Id, @DeviceId, @Action, @Timestamp);
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new
                {
                    Id = $"DA-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    DeviceId = deviceId,
                    Action = action,
                    Timestamp = timestamp
                },
                cancellationToken: ct));

        return result > 0;
    }

    public async Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>>
        GetDeviceActionsAsync(string deviceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, DeviceId, Action, Timestamp
            FROM DeviceActions
            WHERE DeviceId = @DeviceId
            ORDER BY Timestamp DESC;
        """;
        var records = await connection.QueryAsync<(string Id, string DeviceId, string Action, DateTime Timestamp)>(
            new CommandDefinition(sql, new { DeviceId = deviceId }, cancellationToken: ct));

        return records;
    }

    public async Task<bool> LogAlertAsync(string alertId, string type, string message,
        DateTime timestamp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alertId);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(message);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT INTO Alerts (Id, Type, Message, Timestamp)
            VALUES (@Id, @Type, @Message, @Timestamp);
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new
                {
                    Id = alertId,
                    Type = type,
                    Message = message,
                    Timestamp = timestamp
                },
                cancellationToken: ct));

        return result > 0;
    }

    private SqliteConnection GetConnection() => new(_connectionString);

    private class ParkingLotDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Mode { get; set; } = "AUTOMATIC";
    }

    private class ParkingSpotDto
    {
        public string Id { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
        public int IsOccupied { get; set; }
    }

    private class RequestLogDto
    {
        public string Id { get; set; } = string.Empty;
        public string VehiclePlate { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int Approved { get; set; }
    }
}
