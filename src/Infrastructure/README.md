# Infrastructure - Repositorios Segregados y EF Core

## 📋 Overview

Este módulo contiene la implementación de infraestructura con:
- **Repositorios segregados** (ISP - Interface Segregation Principle)
- **DbContext de EF Core** para SQLite
- **Migraciones automáticas** de base de datos
- **Inyección de Dependencias** centralizada

## 🏗️ Estructura

```
Infrastructure/
├── Data/
│   └── ParkingLotDbContext.cs          # DbContext principal
├── Repositories/
│   ├── EFParkingLotRepository.cs       # Queries de estacionamientos
│   ├── EFSpotRepository.cs             # CRUD de espacios
│   ├── EFRequestRepository.cs          # Logging de requests
│   ├── EFSensorRepository.cs           # Logging de sensores
│   ├── EFDeviceActionRepository.cs     # Logging de acciones
│   ├── EFAlertRepository.cs            # Logging de alertas
│   └── CompositeParkingRepository.cs   # Compatibilidad hacia atrás
├── Migrations/
│   ├── 20260511_InitialCreate.cs       # Migration inicial
│   └── ParkingLotDbContextModelSnapshot.cs
├── InfrastructureServiceExtensions.cs  # Extensiones de DI
└── SmartParkingLot.Infrastructure.csproj
```

## 🚀 Uso Rápido

### Opción 1: Con DI Moderna (Recomendado)

```csharp
// En Program.cs
var services = new ServiceCollection();

// Registrar toda la infraestructura de un golpe
services.AddInfrastructure("Data Source=smartparkinglot.db");

// Opcional: Registrar Composite para compatibilidad
services.AddCompositeParkingRepository();

var serviceProvider = services.BuildServiceProvider();

// Inicializar base de datos automáticamente
await serviceProvider.InitializeDatabaseAsync();
```

### Opción 2: Registrar Servicios Individuales

```csharp
services.AddScoped<IParkingLotRepository, EFParkingLotRepository>();
services.AddScoped<ISpotRepository, EFSpotRepository>();
services.AddScoped<IRequestRepository, EFRequestRepository>();
// ... etc
```

## 📊 Interfases Segregadas

| Interfaz | Responsabilidad | Métodos |
|----------|-----------------|---------|
| `IParkingLotRepository` | Queries de estacionamientos | 2 métodos |
| `ISpotRepository` | CRUD de espacios | 5 métodos |
| `IRequestRepository` | Logging de requests | 2 métodos |
| `ISensorRepository` | Logging de sensores | 2 métodos |
| `IDeviceActionRepository` | Logging de acciones | 2 métodos |
| `IAlertRepository` | Logging de alertas | 1 método |

## 🔄 Migraciones

### Crear una nueva migration

```bash
# Desde la raíz del proyecto
dotnet ef migrations add NombreMigration -p src/Infrastructure -s src/Cli
```

### Aplicar migraciones

```bash
# Automáticamente al iniciar (recomendado)
await serviceProvider.InitializeDatabaseAsync();

# O manualmente
dotnet ef database update
```

### Ver estado de migraciones

```bash
dotnet ef migrations list
```

## ✅ Testing de Dependencias

```csharp
[Fact]
public void AllRepositoriesCanBeResolved()
{
    var services = new ServiceCollection();
    services.AddInfrastructure(":memory:"); // SQLite en memoria para tests
    
    var serviceProvider = services.BuildServiceProvider();
    
    // Verificar que todos los repositorios están disponibles
    serviceProvider.GetRequiredService<IParkingLotRepository>();
    serviceProvider.GetRequiredService<ISpotRepository>();
    // ... etc
}
```

## 🔐 Compatibilidad Hacia Atrás

Si el código heredado aún depende de `IParkingRepository`:

```csharp
services.AddInfrastructure("Data Source=smartparkinglot.db");
services.AddCompositeParkingRepository(); // ⚠️ DEPRECATED

// Ahora puedes resolver IParkingRepository para código antiguo
var repo = serviceProvider.GetRequiredService<IParkingRepository>();
```

**NOTA:** `IParkingRepository` está marcada como `[Obsolete]`. Migra a interfaces segregadas cuando sea posible.

## 📋 Entidades de Base de Datos

### Tablas Principales
- `ParkingLots` - Estacionamientos
- `ParkingSpots` - Espacios de estacionamiento

### Tablas de Auditoría
- `RequestLogs` - Historial de requests
- `SensorReadingLogs` - Historial de lecturas de sensores
- `DeviceActionLogs` - Historial de acciones de dispositivos
- `AlertLogs` - Historial de alertas

Todas las tablas tienen **índices optimizados** para queries comunes.

## 🐛 Troubleshooting

### Error: "DbContext options not configured"
```csharp
// ❌ INCORRECTO
var dbContext = new ParkingLotDbContext();

// ✅ CORRECTO - Inyectar desde DI
services.AddDbContext<ParkingLotDbContext>(opts => 
    opts.UseSqlite("..."));
```

### Error: "No migrations found"
```bash
# Crear migration inicial
dotnet ef migrations add InitialCreate -p src/Infrastructure
```

### Error: "Cannot resolve service"
```csharp
// Verifica que registraste el servicio
services.AddInfrastructure(); // Esto incluye todos
```

## 📚 Referencia de Clases

### CompositeParkingRepository
Implementa `IParkingRepository` delegando a todos los repositorios segregados.
- Propósito: Compatibilidad hacia atrás durante migración
- Estado: ⚠️ **DEPRECATED** - Usar interfaces segregadas para código nuevo

### ParkingLotDbContext
Contexto de EF Core para gestionar todas las entidades.
- Configura automáticamente relaciones y índices
- Usa SQLite como proveedor

### InfrastructureServiceExtensions
Métodos de extensión para facilitar el registro de servicios:
- `AddInfrastructure()` - Registra DbContext + todos los repos
- `AddCompositeParkingRepository()` - Registra composite para compatibilidad
- `InitializeDatabaseAsync()` - Aplica migraciones

## 🎯 Próximos Pasos

1. ✅ **Completado:** Repositorios segregados con EF Core
2. ⏳ **Pendiente:** Integrar Infrastructure.csproj en el .sln
3. ⏳ **Pendiente:** Migrar ParkingLotApp a usar DI
4. ⏳ **Pendiente:** Actualizar Cli.csproj con referencia a Infrastructure
5. ⏳ **Pendiente:** Ejecutar tests de validación de dependencias

