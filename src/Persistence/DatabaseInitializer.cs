using Microsoft.Data.Sqlite;
using SmartParkingLot.Core;

namespace SmartParkingLot.Persistence;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await CreateSchemasAsync(connection);

        var hasData = await HasDataAsync(connection);
        if (!hasData)
        {
            await SeedDataAsync(connection);
        }

        connection.Close();
    }

    private async Task CreateSchemasAsync(SqliteConnection connection)
    {
        var createTablesSQL = """
        CREATE TABLE IF NOT EXISTS ParkingLots (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Mode TEXT NOT NULL DEFAULT 'AUTOMATIC',
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ParkingSpots (
            Id TEXT PRIMARY KEY,
            LotId TEXT NOT NULL,
            Address TEXT NOT NULL,
            Type TEXT NOT NULL,
            Floor TEXT NOT NULL,
            IsOccupied BOOLEAN DEFAULT 0,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (LotId) REFERENCES ParkingLots(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS RequestLogs (
            Id TEXT PRIMARY KEY,
            VehiclePlate TEXT NOT NULL,
            RequestType TEXT NOT NULL,
            LotId TEXT NOT NULL,
            ReleasedSpotId TEXT,
            Timestamp DATETIME NOT NULL,
            Approved BOOLEAN NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (LotId) REFERENCES ParkingLots(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS SensorReadings (
            Id TEXT PRIMARY KEY,
            SensorId TEXT NOT NULL,
            Value TEXT NOT NULL,
            Timestamp DATETIME NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS DeviceActions (
            Id TEXT PRIMARY KEY,
            DeviceId TEXT NOT NULL,
            Action TEXT NOT NULL,
            Timestamp DATETIME NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS Alerts (
            Id TEXT PRIMARY KEY,
            Type TEXT NOT NULL,
            Message TEXT NOT NULL,
            Timestamp DATETIME NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX IF NOT EXISTS IX_ParkingSpots_LotId ON ParkingSpots(LotId);
        CREATE INDEX IF NOT EXISTS IX_ParkingSpots_IsOccupied ON ParkingSpots(IsOccupied);
        CREATE INDEX IF NOT EXISTS IX_RequestLogs_VehiclePlate ON RequestLogs(VehiclePlate);
        CREATE INDEX IF NOT EXISTS IX_RequestLogs_LotId ON RequestLogs(LotId);
        CREATE INDEX IF NOT EXISTS IX_RequestLogs_ReleasedSpotId ON RequestLogs(ReleasedSpotId);
        CREATE INDEX IF NOT EXISTS IX_SensorReadings_SensorId ON SensorReadings(SensorId);
        CREATE INDEX IF NOT EXISTS IX_SensorReadings_Timestamp ON SensorReadings(Timestamp);
        CREATE INDEX IF NOT EXISTS IX_DeviceActions_DeviceId ON DeviceActions(DeviceId);
        CREATE INDEX IF NOT EXISTS IX_DeviceActions_Timestamp ON DeviceActions(Timestamp);
        CREATE INDEX IF NOT EXISTS IX_Alerts_Timestamp ON Alerts(Timestamp);
        CREATE INDEX IF NOT EXISTS IX_Alerts_Type ON Alerts(Type);
        """;

        using var command = connection.CreateCommand();
        command.CommandText = createTablesSQL;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> HasDataAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ParkingLots;";
        var result = await command.ExecuteScalarAsync();
        return result is long count && count > 0;
    }

    private async Task SeedDataAsync(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            var lotId = "LOT-01";
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO ParkingLots (Id, Name, Mode)
                    VALUES (@Id, @Name, @Mode);
                """;
                command.Parameters.AddWithValue("@Id", lotId);
                command.Parameters.AddWithValue("@Name", "Campus Barcelona");
                command.Parameters.AddWithValue("@Mode", "AUTOMATIC");
                await command.ExecuteNonQueryAsync();
            }

            var spots = new[]
            {
                ("A1", "Zona-A Fila-1", "Estándar", "Planta Baja"),
                ("A2", "Zona-A Fila-2", "Estándar", "Planta Baja"),
                ("A3", "Zona-A Fila-3", "Estándar", "Planta Baja"),
            };

            foreach (var (spotId, address, type, floor) in spots)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO ParkingSpots (Id, LotId, Address, Type, Floor, IsOccupied)
                    VALUES (@Id, @LotId, @Address, @Type, @Floor, 0);
                """;
                command.Parameters.AddWithValue("@Id", spotId);
                command.Parameters.AddWithValue("@LotId", lotId);
                command.Parameters.AddWithValue("@Address", address);
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@Floor", floor);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            Console.WriteLine("[Database] ✓ Schema creado y seeding completado exitosamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[Database] ✗ Error en seeding: {ex.Message}");
            throw;
        }
    }
}
