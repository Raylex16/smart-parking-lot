using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;
using SmartParkingLot.Infrastructure.Repositories;

namespace SmartParkingLot.Infrastructure;

/// <summary>
/// Extensiones para registrar servicios de infraestructura en el contenedor de DI.
/// 
/// Uso:
///     var services = new ServiceCollection();
///     services.AddInfrastructure("Data Source=smartparkinglot.db");
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra DbContext y todos los repositorios segregados en el contenedor de DI.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString = "Data Source=smartparkinglot.db")
    {
        // Registrar DbContext
        services.AddDbContext<ParkingLotDbContext>(options =>
            options.UseSqlite(connectionString));

        // Registrar repositorios segregados
        services.AddScoped<IParkingLotRepository, EFParkingLotRepository>();
        services.AddScoped<ISpotRepository, EFSpotRepository>();
        services.AddScoped<IRequestRepository, EFRequestRepository>();
        services.AddScoped<ISensorRepository, EFSensorRepository>();
        services.AddScoped<IDeviceActionRepository, EFDeviceActionRepository>();
        services.AddScoped<IAlertRepository, EFAlertRepository>();

        return services;
    }

    /// <summary>
    /// Registra el Composite Repository para soporte a código heredado.
    /// 
    /// NOTA: Usa esta opción solo si hay código que aún depende de IParkingRepository.
    /// Para código nuevo, usa los repositorios segregados directamente.
    /// </summary>
    public static IServiceCollection AddCompositeParkingRepository(
        this IServiceCollection services)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddScoped<IParkingRepository>(provider =>
            new CompositeParkingRepository(
                provider.GetRequiredService<IParkingLotRepository>(),
                provider.GetRequiredService<ISpotRepository>(),
                provider.GetRequiredService<IRequestRepository>(),
                provider.GetRequiredService<ISensorRepository>(),
                provider.GetRequiredService<IDeviceActionRepository>(),
                provider.GetRequiredService<IAlertRepository>()));
#pragma warning restore CS0618
        return services;
    }

    /// <summary>
    /// Aplica las migraciones pendientes y crea la base de datos si es necesaria.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingLotDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Inserta datos iniciales si la base de datos está vacía.
    /// </summary>
    public static async Task SeedInitialDataAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingLotDbContext>();

        if (!await dbContext.ParkingLots.AnyAsync())
        {
            dbContext.ParkingLots.Add(new SmartParkingLot.Core.ParkingLot("LOT-01", "Campus Barcelona"));
            await dbContext.SaveChangesAsync();
        }
    }
}
