using System.Data.SQLite;
using SmartParkingLot.Core;

namespace SmartParkingLot.Persistence;

/// <summary>
/// GRASP - Pure Fabrication: Clase artificial que no es parte del dominio,
/// responsable únicamente de inicializar la base de datos SQLite.
/// 
/// Encapsula la lógica de creación de schema y seeding programático.
/// </summary>
public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>Inicializa la base de datos: crea schema y ejecuta seeding si es necesario.</summary>
    public async Task InitializeAsync()
    {
        // Crear conexión y asegurar que la BD existe
        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync();

        // Crear tablas
        await CreateSchemasAsync(connection);

        // Verificar si hay datos; si no, hacer seeding
        var hasData = await HasDataAsync(connection);
        if (!hasData)
        {
            await SeedDataAsync(connection);
        }

        connection.Close();
    }

    /// <summary>Crea el schema de las tablas.</summary>
    private async Task CreateSchemasAsync(SQLiteConnection connection)
    {
        var createTablesSQL = """
        -- Tabla para lotes de estacionamiento
        CREATE TABLE IF NOT EXISTS ParkingLots (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Mode TEXT NOT NULL DEFAULT 'AUTOMATIC',
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        -- Tabla para espacios de estacionamiento
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

        -- Tabla de auditoría: historial de requests (entrada/salida)
        CREATE TABLE IF NOT EXISTS RequestLogs (
            Id TEXT PRIMARY KEY,
            VehiclePlate TEXT NOT NULL,
            RequestType TEXT NOT NULL,  -- 'ENTRY' o 'EXIT'
            LotId TEXT NOT NULL,
            Timestamp DATETIME NOT NULL,
            Approved BOOLEAN NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (LotId) REFERENCES ParkingLots(Id) ON DELETE CASCADE
        );

        -- Tabla: Lecturas de sensores (Rúbrica - Valor + Timestamp)
        CREATE TABLE IF NOT EXISTS SensorReadings (
            Id TEXT PRIMARY KEY,
            SensorId TEXT NOT NULL,
            Value TEXT NOT NULL,  -- Valor leído (ej: "true", "distance:15cm", "plate:VH-001")
            Timestamp DATETIME NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        -- Tabla: Acciones de dispositivos (Rúbrica - LED ON/OFF, GATE, etc.)
        CREATE TABLE IF NOT EXISTS DeviceActions (
            Id TEXT PRIMARY KEY,
            DeviceId TEXT NOT NULL,  -- Ej: 'LED_1', 'GATE_G-01', 'ACTUATOR_1'
            Action TEXT NOT NULL,    -- 'ON', 'OFF', 'OPEN_90deg', etc.
            Timestamp DATETIME NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        );

        -- Índices para mejorar performance en búsquedas frecuentes
        CREATE INDEX IF NOT EXISTS IX_ParkingSpots_LotId ON ParkingSpots(LotId);
        CREATE INDEX IF NOT EXISTS IX_ParkingSpots_IsOccupied ON ParkingSpots(IsOccupied);
        CREATE INDEX IF NOT EXISTS IX_RequestLogs_VehiclePlate ON RequestLogs(VehiclePlate);
        CREATE INDEX IF NOT EXISTS IX_RequestLogs_LotId ON RequestLogs(LotId);
        CREATE INDEX IF NOT EXISTS IX_SensorReadings_SensorId ON SensorReadings(SensorId);
        CREATE INDEX IF NOT EXISTS IX_SensorReadings_Timestamp ON SensorReadings(Timestamp);
        CREATE INDEX IF NOT EXISTS IX_DeviceActions_DeviceId ON DeviceActions(DeviceId);
        CREATE INDEX IF NOT EXISTS IX_DeviceActions_Timestamp ON DeviceActions(Timestamp);
        """;

        using var command = connection.CreateCommand();
        command.CommandText = createTablesSQL;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Verifica si la BD ya contiene datos.</summary>
    private async Task<bool> HasDataAsync(SQLiteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ParkingLots;";
        var result = await command.ExecuteScalarAsync();
        return result is long count && count > 0;
    }

    /// <summary>Realiza el seeding inicial de datos (GRASP - Creator).</summary>
    private async Task SeedDataAsync(SQLiteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Insertar lotes de estacionamiento
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

            // 2. Insertar espacios de estacionamiento
            var spots = new[]
            {
                ("A1", "Zona-A Fila-1", "Estándar", "Planta Baja"),
                ("A2", "Zona-A Fila-2", "Estándar", "Planta Baja"),
                ("B1", "Zona-B Fila-1", "Compacto", "Nivel 1"),
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
