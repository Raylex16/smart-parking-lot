// ============================================================================
// EJEMPLO: Program.cs Con DI Completa (Referencia para migración futura)
// ============================================================================
// Este archivo es un EJEMPLO de cómo se vería Program.cs una vez completamente
// migrado a inyección de dependencias. NO ejecutar directamente aún.

using Microsoft.Extensions.DependencyInjection;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Application;
using SmartParkingLot.Application.Display;
using SmartParkingLot.Application.Logging;
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Infrastructure;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Cli.Examples;

/*
public class DIConfigurationExample
{
    public static async Task RunAsync()
    {
        // 1. Crear la colección de servicios
        var services = new ServiceCollection();

        // 2. Registrar Infraestructura (DbContext + Repositorios)
        services.AddInfrastructure("Data Source=smartparkinglot.db");

        // 3. Registrar servicios de aplicación
        services.AddScoped<ILogger>(provider => 
            new CompositeLogger(
                new ConsoleLogger(LogLevel.Info),
                new FileLogger("logs", LogLevel.Debug)));

        services.AddScoped<ICapacityService>(provider =>
            new CapacityService(
                new ParkingLot("LOT_ID", "Main Parking"),
                provider.GetRequiredService<ISpotRepository>(),
                provider.GetRequiredService<ILogger>()));

        services.AddScoped<IAlertService>(provider =>
            new AlertService(
                provider.GetRequiredService<ILogger>(),
                provider.GetRequiredService<IAlertRepository>()));

        services.AddScoped<IAccessPolicy, AlwaysAllowPolicy>();

        services.AddScoped<IGateRequestHandler, GateController>(provider =>
            new GateController(
                provider.GetRequiredService<ISpotRepository>(),
                provider.GetRequiredService<IRequestRepository>(),
                provider.GetRequiredService<ICapacityService>(),
                provider.GetRequiredService<IAlertService>(),
                provider.GetRequiredService<IAccessPolicy>(),
                provider.GetRequiredService<ILogger>()));

        // 4. Construir el contenedor
        var serviceProvider = services.BuildServiceProvider();

        // 5. Inicializar la base de datos
        await serviceProvider.InitializeDatabaseAsync();

        // 6. Resolver y ejecutar la aplicación
        var app = serviceProvider.GetRequiredService<IGateRequestHandler>();
        
        // Usar la aplicación...
    }
}

// Ventajas de esta configuración:
// ✅ Una sola fuente de verdad para la configuración de dependencias
// ✅ Fácil de testear (reemplaza servicios en tests)
// ✅ Servicios registrados solo si se usan
// ✅ Ciclo de vida explícito (AddScoped, AddSingleton, etc.)
// ✅ DbContext gestionado automáticamente
// ✅ Migraciones aplicadas automáticamente
// ✅ Segregación clara de responsabilidades (cada servicio depende de lo que necesita)
*/
