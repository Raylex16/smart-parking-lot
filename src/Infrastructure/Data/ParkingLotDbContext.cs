using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core;

namespace SmartParkingLot.Infrastructure.Data;

public class ParkingLotDbContext : DbContext
{
    public ParkingLotDbContext(DbContextOptions<ParkingLotDbContext> options) : base(options)
    {
    }

    // Domain Entities
    public DbSet<ParkingLot> ParkingLots { get; set; }
    public DbSet<ParkingSpot> ParkingSpots { get; set; }

    // Audit & History Entities
    public DbSet<RequestLog> RequestLogs { get; set; }
    public DbSet<SensorReadingLog> SensorReadingLogs { get; set; }
    public DbSet<DeviceActionLog> DeviceActionLogs { get; set; }
    public DbSet<AlertLog> AlertLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=smartparkinglot.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ParkingLot configuration
        modelBuilder.Entity<ParkingLot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Mode).IsRequired().HasConversion<string>();
            // Sin navigation property: los spots se cargan manualmente en el repositorio
            entity.HasMany<ParkingSpot>()
                .WithOne()
                .HasForeignKey("LotId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ParkingSpot configuration
        modelBuilder.Entity<ParkingSpot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Address).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Floor).IsRequired();
            entity.Property(e => e.IsOccupied).IsRequired();
            entity.HasIndex(e => e.IsOccupied).HasDatabaseName("idx_spot_occupancy");
        });

        // RequestLog configuration
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestId).IsRequired();
            entity.Property(e => e.VehiclePlate).IsRequired();
            entity.HasIndex(e => e.VehiclePlate).HasDatabaseName("idx_request_plate");
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_request_timestamp");
        });

        // SensorReadingLog configuration
        modelBuilder.Entity<SensorReadingLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SensorId).IsRequired();
            entity.HasIndex(e => e.SensorId).HasDatabaseName("idx_sensor_id");
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_sensor_timestamp");
        });

        // DeviceActionLog configuration
        modelBuilder.Entity<DeviceActionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).IsRequired();
            entity.HasIndex(e => e.DeviceId).HasDatabaseName("idx_device_id");
        });

        // AlertLog configuration
        modelBuilder.Entity<AlertLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_alert_timestamp");
        });
    }
}
