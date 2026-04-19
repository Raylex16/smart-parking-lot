using System.Data;
using System.Data.SQLite;
using Dapper;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Persistence;

/// <summary>
/// SOLID - Dependency Inversion Principle (DIP):
/// Implementación concreta de IParkingRepository usando Dapper + SQLite.
/// Los consumidores dependen de IParkingRepository, no de esta clase.
/// 
/// GRASP - Low Coupling: Encapsula toda la complejidad SQL y la comunicación con BD.
/// La lógica de dominio no conoce detalles de persistencia.
/// 
/// Patrón: Repository — Actúa como una colección en memoria de entidades.
/// </summary>
public class SqliteParkingRepository : IParkingRepository
{
    private readonly string _connectionString;

    public SqliteParkingRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Operaciones sobre ParkingLot
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        // Obtener el lote
        var sql = "SELECT Id, Name, Mode FROM ParkingLots WHERE Id = @LotId;";
        var lotDto = await connection.QueryFirstOrDefaultAsync<ParkingLotDto>(
            new CommandDefinition(sql, new { LotId = lotId }, cancellationToken: ct));

        if (lotDto is null) return null;

        // Obtener todos sus espacios
        var spotsSql = "SELECT Id, Address, Type, Floor, IsOccupied FROM ParkingSpots WHERE LotId = @LotId;";
        var spotsDto = await connection.QueryAsync<ParkingSpotDto>(
            new CommandDefinition(spotsSql, new { LotId = lotId }, cancellationToken: ct));

        // Reconstruir agregado: ParkingLot + ParkingSpots (GRASP - Information Expert)
        var lot = new ParkingLot(lotDto.Id, lotDto.Name, Enum.Parse<ParkingMode>(lotDto.Mode));

        foreach (var spotDto in spotsDto)
        {
            var spot = new ParkingSpot(spotDto.Id, spotDto.Address, spotDto.Type, spotDto.Floor);
            // Si estaba ocupado en BD, restaurar estado
            if (spotDto.IsOccupied == 1)
            {
                try { spot.Occupy(); } catch { /* Ignorar si ya está ocupado */ }
            }
            lot.AddSpot(spot);
        }

        return lot;
    }

    public async Task<IEnumerable<ParkingLot>> GetAllParkingLotsAsync(CancellationToken ct = default)
    {
        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = "SELECT Id, Name, Mode FROM ParkingLots;";
        var lotsDto = await connection.QueryAsync<ParkingLotDto>(
            new CommandDefinition(sql, cancellationToken: ct));

        var lots = new List<ParkingLot>();

        foreach (var lotDto in lotsDto)
        {
            var spotsSql = "SELECT Id, Address, Type, Floor, IsOccupied FROM ParkingSpots WHERE LotId = @LotId;";
            var spotsDto = await connection.QueryAsync<ParkingSpotDto>(
                new CommandDefinition(spotsSql, new { LotId = lotDto.Id }, cancellationToken: ct));

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

            lots.Add(lot);
        }

        return lots;
    }

    public async Task<bool> AddParkingLotAsync(ParkingLot lot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lot);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        using var transaction = connection.BeginTransaction();

        try
        {
            // Insertar el lote
            var lotSql = """
                INSERT INTO ParkingLots (Id, Name, Mode)
                VALUES (@Id, @Name, @Mode);
            """;
            await connection.ExecuteAsync(
                new CommandDefinition(lotSql, 
                    new { lot.Id, lot.Name, Mode = lot.Mode.ToString() }, 
                    transaction, cancellationToken: ct));

            // Insertar sus espacios
            var spots = lot.GetSpots();
            if (spots.Count > 0)
            {
                var spotSql = """
                    INSERT INTO ParkingSpots (Id, LotId, Address, Type, Floor, IsOccupied)
                    VALUES (@Id, @LotId, @Address, @Type, @Floor, @IsOccupied);
                """;

                foreach (var spot in spots)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(spotSql,
                            new
                            {
                                spot.Id,
                                LotId = lot.Id,
                                spot.Address,
                                spot.Type,
                                spot.Floor,
                                IsOccupied = spot.IsOccupied ? 1 : 0
                            },
                            transaction, cancellationToken: ct));
                }
            }

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> UpdateParkingLotAsync(ParkingLot lot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lot);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            UPDATE ParkingLots 
            SET Name = @Name, Mode = @Mode
            WHERE Id = @Id;
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new { lot.Id, lot.Name, Mode = lot.Mode.ToString() },
                cancellationToken: ct));

        return result > 0;
    }

    public async Task<bool> DeleteParkingLotAsync(string lotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = "DELETE FROM ParkingLots WHERE Id = @LotId;";
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { LotId = lotId }, cancellationToken: ct));

        return result > 0;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Operaciones sobre ParkingSpot
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ParkingSpot?> GetParkingSpotByIdAsync(string spotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = "SELECT Id, Address, Type, Floor, IsOccupied FROM ParkingSpots WHERE Id = @SpotId;";
        var spotDto = await connection.QueryFirstOrDefaultAsync<ParkingSpotDto>(
            new CommandDefinition(sql, new { SpotId = spotId }, cancellationToken: ct));

        if (spotDto is null) return null;

        var spot = new ParkingSpot(spotDto.Id, spotDto.Address, spotDto.Type, spotDto.Floor);
        if (spotDto.IsOccupied == 1)
        {
            try { spot.Occupy(); } catch { }
        }

        return spot;
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

    public async Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, Address, Type, Floor, IsOccupied 
            FROM ParkingSpots 
            WHERE LotId = @LotId AND IsOccupied = 0;
        """;
        var spotsDto = await connection.QueryAsync<ParkingSpotDto>(
            new CommandDefinition(sql, new { LotId = lotId }, cancellationToken: ct));

        return spotsDto.Select(dto =>
            new ParkingSpot(dto.Id, dto.Address, dto.Type, dto.Floor)
        ).ToList();
    }

    public async Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(string lotId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, Address, Type, Floor, IsOccupied 
            FROM ParkingSpots 
            WHERE LotId = @LotId AND IsOccupied = 1;
        """;
        var spotsDto = await connection.QueryAsync<ParkingSpotDto>(
            new CommandDefinition(sql, new { LotId = lotId }, cancellationToken: ct));

        var spots = new List<ParkingSpot>();

        foreach (var spotDto in spotsDto)
        {
            var spot = new ParkingSpot(spotDto.Id, spotDto.Address, spotDto.Type, spotDto.Floor);
            try { spot.Occupy(); } catch { }
            spots.Add(spot);
        }

        return spots;
    }

    public async Task<bool> AddParkingSpotAsync(string lotId, ParkingSpot spot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lotId);
        ArgumentNullException.ThrowIfNull(spot);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT INTO ParkingSpots (Id, LotId, Address, Type, Floor, IsOccupied)
            VALUES (@Id, @LotId, @Address, @Type, @Floor, @IsOccupied);
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new
                {
                    spot.Id,
                    LotId = lotId,
                    spot.Address,
                    spot.Type,
                    spot.Floor,
                    IsOccupied = spot.IsOccupied ? 1 : 0
                },
                cancellationToken: ct));

        return result > 0;
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

    // ═══════════════════════════════════════════════════════════════════
    // Operaciones de auditoría: RequestLogs
    // ═══════════════════════════════════════════════════════════════════

    public async Task<bool> LogRequestAsync(string requestId, string vehiclePlate, string requestType,
        string lotId, DateTime timestamp, bool approved, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(vehiclePlate);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(lotId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            INSERT INTO RequestLogs (Id, VehiclePlate, RequestType, LotId, Timestamp, Approved)
            VALUES (@Id, @VehiclePlate, @RequestType, @LotId, @Timestamp, @Approved);
        """;
        var result = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new
                {
                    Id = requestId,
                    VehiclePlate = vehiclePlate,
                    RequestType = requestType,
                    LotId = lotId,
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

    // ═══════════════════════════════════════════════════════════════════
    // Lecturas de Sensores (Rúbrica)
    // ═══════════════════════════════════════════════════════════════════

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

    public async Task<IEnumerable<(string Id, string SensorId, string Value, DateTime Timestamp)>> 
        GetSensorReadingsByDateRangeAsync(string sensorId, DateTime startDate, DateTime endDate, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sensorId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, SensorId, Value, Timestamp
            FROM SensorReadings
            WHERE SensorId = @SensorId AND Timestamp BETWEEN @StartDate AND @EndDate
            ORDER BY Timestamp DESC;
        """;
        var records = await connection.QueryAsync<(string Id, string SensorId, string Value, DateTime Timestamp)>(
            new CommandDefinition(sql,
                new { SensorId = sensorId, StartDate = startDate, EndDate = endDate },
                cancellationToken: ct));

        return records;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Acciones de Dispositivos (Rúbrica)
    // ═══════════════════════════════════════════════════════════════════

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

    public async Task<IEnumerable<(string Id, string DeviceId, string Action, DateTime Timestamp)>> 
        GetDeviceActionsByDateRangeAsync(string deviceId, DateTime startDate, DateTime endDate, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        using var connection = GetConnection();
        await connection.OpenAsync(ct);

        var sql = """
            SELECT Id, DeviceId, Action, Timestamp
            FROM DeviceActions
            WHERE DeviceId = @DeviceId AND Timestamp BETWEEN @StartDate AND @EndDate
            ORDER BY Timestamp DESC;
        """;
        var records = await connection.QueryAsync<(string Id, string DeviceId, string Action, DateTime Timestamp)>(
            new CommandDefinition(sql,
                new { DeviceId = deviceId, StartDate = startDate, EndDate = endDate },
                cancellationToken: ct));

        return records;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers privados
    // ═══════════════════════════════════════════════════════════════════

    private SQLiteConnection GetConnection() => new(_connectionString);

    // ─── DTOs para mapeo con Dapper ───
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

    private class SensorReadingDto
    {
        public string Id { get; set; } = string.Empty;
        public string SensorId { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    private class DeviceActionDto
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
