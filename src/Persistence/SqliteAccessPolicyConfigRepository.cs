using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Persistence;

public sealed class SqliteAccessPolicyConfigRepository : IAccessPolicyConfigRepository
{
    private readonly string _connectionString;

    public SqliteAccessPolicyConfigRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<AccessPolicyConfig?> GetByLotIdAsync(string lotId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var row = await connection.QueryFirstOrDefaultAsync<AccessPolicyConfigDto>(
            new CommandDefinition(
                "SELECT LotId, AllowedPlatesJson, ScheduleStart, ScheduleEnd FROM AccessPolicyConfigs WHERE LotId = @LotId;",
                new { LotId = lotId },
                cancellationToken: ct));

        if (row is null) return null;

        var config = new AccessPolicyConfig(row.LotId);
        var plates = JsonSerializer.Deserialize<List<string>>(row.AllowedPlatesJson) ?? [];
        config.SetWhitelist(plates);
        config.SetSchedule(TimeSpan.Parse(row.ScheduleStart), TimeSpan.Parse(row.ScheduleEnd));
        return config;
    }

    public async Task SaveAsync(AccessPolicyConfig config, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var platesJson = JsonSerializer.Serialize(config.AllowedPlates);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO AccessPolicyConfigs (LotId, AllowedPlatesJson, ScheduleStart, ScheduleEnd)
                VALUES (@LotId, @PlatesJson, @Start, @End)
                ON CONFLICT(LotId) DO UPDATE SET
                    AllowedPlatesJson = excluded.AllowedPlatesJson,
                    ScheduleStart     = excluded.ScheduleStart,
                    ScheduleEnd       = excluded.ScheduleEnd;
                """,
                new
                {
                    LotId     = config.LotId,
                    PlatesJson = platesJson,
                    Start     = config.ScheduleStart.ToString(),
                    End       = config.ScheduleEnd.ToString()
                },
                cancellationToken: ct));
    }

    private record AccessPolicyConfigDto(
        string LotId,
        string AllowedPlatesJson,
        string ScheduleStart,
        string ScheduleEnd);
}
