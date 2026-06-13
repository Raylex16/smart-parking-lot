# F1: Access Policy Backend — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ampliar `ParkingMode` a 4 valores, persistir la configuración de políticas (whitelist + horario) en SQLite, e introducir `AccessPolicyFactory` y `AccessPolicyConfigService` para que `ParkingModeService` construya la política activa dinámicamente.

**Architecture:** `AccessPolicyConfig` es una entidad de Core ligada a un lote. `IAccessPolicyFactory` reemplaza el `Func<ParkingMode,IAccessPolicy>` inline. `AccessPolicyConfigService` orquesta persistencia + reaplicación de política en caliente. `ParkingModeService` delega la construcción al factory.

**Tech Stack:** C# 14 / .NET 10, Entity Framework Core (SQLite), xUnit, `System.Text.Json` para serializar la whitelist.

---

## Mapa de archivos

| Acción | Archivo |
|--------|---------|
| Modify | `src/Core/Enums/ParkingMode.cs` |
| Create | `src/Core/Entities/AccessPolicyConfig.cs` |
| Create | `src/Core/Interfaces/IAccessPolicyConfigRepository.cs` |
| Create | `src/Core/Interfaces/IAccessPolicyConfigService.cs` |
| Create | `src/Application/Policies/AccessPolicyFactory.cs` |
| Create | `src/Application/Services/AccessPolicyConfigService.cs` |
| Modify | `src/Application/Services/ParkingModeService.cs` |
| Modify | `src/Application/Bootstrap/ApplicationModule.cs` |
| Modify | `src/Infrastructure/Data/ParkingLotDbContext.cs` |
| Create | `src/Infrastructure/Repositories/EFAccessPolicyConfigRepository.cs` |
| Create | `src/Infrastructure/Migrations/<timestamp>_AddAccessPolicyConfig.cs` (generado por EF) |
| Create | `tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs` |
| Create | `tests/SmartParkingLot.Tests/Application/AccessPolicyConfigServiceTests.cs` |

---

### Task 1: Ampliar `ParkingMode` a 4 valores

**Files:**
- Modify: `src/Core/Enums/ParkingMode.cs`
- Test: `tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs`

- [ ] **Step 1: Escribir el test que verifica que los 4 valores existen**

```csharp
// tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs
using SmartParkingLot.Core;

namespace SmartParkingLot.Tests.Application;

public class AccessPolicyFactoryTests
{
    [Theory]
    [InlineData(ParkingMode.AUTOMATIC)]
    [InlineData(ParkingMode.MANUAL)]
    [InlineData(ParkingMode.RESTRICTED)]
    [InlineData(ParkingMode.SCHEDULED)]
    public void All_four_modes_are_defined(ParkingMode mode)
    {
        Assert.True(Enum.IsDefined(mode));
    }
}
```

- [ ] **Step 2: Ejecutar el test — debe FALLAR porque RESTRICTED/SCHEDULED no existen**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyFactoryTests" -v minimal
```
Esperado: errores de compilación `ParkingMode.RESTRICTED` y `ParkingMode.SCHEDULED` no definidos.

- [ ] **Step 3: Añadir los 2 valores nuevos al enum**

```csharp
// src/Core/Enums/ParkingMode.cs
namespace SmartParkingLot.Core;

public enum ParkingMode
{
    AUTOMATIC,
    MANUAL,
    RESTRICTED,
    SCHEDULED
}
```

- [ ] **Step 4: Ejecutar el test — debe PASAR**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyFactoryTests" -v minimal
```
Esperado: 4 tests pasan.

- [ ] **Step 5: Commit**

```
git add src/Core/Enums/ParkingMode.cs tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs
git commit -m "feat(core): ampliar ParkingMode a 4 valores (RESTRICTED, SCHEDULED)"
```

---

### Task 2: Entidad `AccessPolicyConfig`

**Files:**
- Create: `src/Core/Entities/AccessPolicyConfig.cs`

- [ ] **Step 1: Añadir test de comportamiento de la entidad a `AccessPolicyFactoryTests.cs`**

```csharp
// Añadir al final de AccessPolicyFactoryTests.cs, dentro de la clase:

[Fact]
public void AccessPolicyConfig_SetWhitelist_replaces_plates()
{
    var config = new AccessPolicyConfig("LOT-1");
    config.SetWhitelist(["ABC-123", "XYZ-789"]);
    Assert.Equal(2, config.AllowedPlates.Count);
    Assert.Contains("ABC-123", config.AllowedPlates);
}

[Fact]
public void AccessPolicyConfig_SetSchedule_stores_times()
{
    var config = new AccessPolicyConfig("LOT-1");
    config.SetSchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(20));
    Assert.Equal(TimeSpan.FromHours(8),  config.ScheduleStart);
    Assert.Equal(TimeSpan.FromHours(20), config.ScheduleEnd);
}
```

- [ ] **Step 2: Ejecutar — debe FALLAR (tipo no existe)**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyFactoryTests" -v minimal
```
Esperado: error de compilación `AccessPolicyConfig` no encontrado.

- [ ] **Step 3: Crear la entidad**

```csharp
// src/Core/Entities/AccessPolicyConfig.cs
namespace SmartParkingLot.Core.Entities;

public class AccessPolicyConfig
{
    public string LotId { get; private set; }
    public IReadOnlyList<string> AllowedPlates => _plates.AsReadOnly();
    public TimeSpan ScheduleStart { get; private set; } = TimeSpan.FromHours(8);
    public TimeSpan ScheduleEnd   { get; private set; } = TimeSpan.FromHours(20);

    private List<string> _plates = [];

    // Constructor para EF
    private AccessPolicyConfig() { LotId = ""; }

    public AccessPolicyConfig(string lotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotId);
        LotId = lotId;
    }

    public void SetWhitelist(IEnumerable<string> plates)
    {
        _plates = plates
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public void SetSchedule(TimeSpan start, TimeSpan end)
    {
        ScheduleStart = start;
        ScheduleEnd   = end;
    }
}
```

- [ ] **Step 4: Ejecutar — debe PASAR**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyFactoryTests" -v minimal
```
Esperado: 6 tests pasan.

- [ ] **Step 5: Commit**

```
git add src/Core/Entities/AccessPolicyConfig.cs tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs
git commit -m "feat(core): entidad AccessPolicyConfig (whitelist + horario)"
```

---

### Task 3: Interfaz `IAccessPolicyConfigRepository`

**Files:**
- Create: `src/Core/Interfaces/IAccessPolicyConfigRepository.cs`

- [ ] **Step 1: Crear la interfaz**

```csharp
// src/Core/Interfaces/IAccessPolicyConfigRepository.cs
using SmartParkingLot.Core.Entities;

namespace SmartParkingLot.Core.Interfaces;

public interface IAccessPolicyConfigRepository
{
    Task<AccessPolicyConfig?> GetByLotIdAsync(string lotId, CancellationToken ct = default);
    Task SaveAsync(AccessPolicyConfig config, CancellationToken ct = default);
}
```

- [ ] **Step 2: Compilar para verificar que no hay errores**

```
dotnet build src/Core/SmartParkingLot.Core.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/Core/Interfaces/IAccessPolicyConfigRepository.cs
git commit -m "feat(core): interfaz IAccessPolicyConfigRepository"
```

---

### Task 4: `AccessPolicyFactory`

**Files:**
- Create: `src/Application/Policies/AccessPolicyFactory.cs`
- Test: `tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs`

- [ ] **Step 1: Añadir tests del factory al archivo existente**

```csharp
// Añadir dentro de la clase AccessPolicyFactoryTests:

[Fact]
public void Factory_AUTOMATIC_returns_AlwaysAllowPolicy()
{
    var factory = BuildFactory();
    var config  = new AccessPolicyConfig("LOT-1");
    var policy  = factory.Create(ParkingMode.AUTOMATIC, config);
    Assert.IsType<AlwaysAllowPolicy>(policy);
}

[Fact]
public void Factory_MANUAL_returns_ManualAccessPolicy()
{
    var factory = BuildFactory();
    var config  = new AccessPolicyConfig("LOT-1");
    var policy  = factory.Create(ParkingMode.MANUAL, config);
    Assert.IsType<ManualAccessPolicy>(policy);
}

[Fact]
public void Factory_RESTRICTED_returns_RestrictedAccessPolicy()
{
    var factory = BuildFactory();
    var config  = new AccessPolicyConfig("LOT-1");
    config.SetWhitelist(["ABC-123"]);
    var policy  = factory.Create(ParkingMode.RESTRICTED, config);
    Assert.IsType<RestrictedAccessPolicy>(policy);
}

[Fact]
public void Factory_SCHEDULED_returns_ScheduledBasedPolicy()
{
    var factory = BuildFactory();
    var config  = new AccessPolicyConfig("LOT-1");
    config.SetSchedule(TimeSpan.FromHours(8), TimeSpan.FromHours(20));
    var policy  = factory.Create(ParkingMode.SCHEDULED, config);
    Assert.IsType<ScheduledBasedPolicy>(policy);
}

private static AccessPolicyFactory BuildFactory()
{
    var queue  = new InMemoryApprovalQueue();
    var logger = new NullLogger();
    return new AccessPolicyFactory(queue, logger, TimeSpan.FromSeconds(30));
}

// Stub mínimo de ILogger para tests
private sealed class NullLogger : SmartParkingLot.Core.Interfaces.ILogger
{
    public void Debug(string source, string msg) { }
    public void Info(string source, string msg)  { }
    public void Warn(string source, string msg)  { }
    public void Error(string source, string msg) { }
}
```

- [ ] **Step 2: Ejecutar — debe FALLAR (AccessPolicyFactory no existe)**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyFactoryTests" -v minimal
```
Esperado: error de compilación.

- [ ] **Step 3: Crear el factory**

```csharp
// src/Application/Policies/AccessPolicyFactory.cs
using SmartParkingLot.Core;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;

public interface IAccessPolicyFactory
{
    IAccessPolicy Create(ParkingMode mode, AccessPolicyConfig config);
}

public sealed class AccessPolicyFactory : IAccessPolicyFactory
{
    private readonly IApprovalQueue _queue;
    private readonly ILogger        _logger;
    private readonly TimeSpan       _manualTimeout;

    public AccessPolicyFactory(IApprovalQueue queue, ILogger logger, TimeSpan manualTimeout)
    {
        _queue         = queue;
        _logger        = logger;
        _manualTimeout = manualTimeout;
    }

    public IAccessPolicy Create(ParkingMode mode, AccessPolicyConfig config) => mode switch
    {
        ParkingMode.AUTOMATIC  => new AlwaysAllowPolicy(),
        ParkingMode.MANUAL     => new ManualAccessPolicy(_queue, _logger, _manualTimeout),
        ParkingMode.RESTRICTED => new RestrictedAccessPolicy(config.AllowedPlates, _logger),
        ParkingMode.SCHEDULED  => new ScheduledBasedPolicy(config.ScheduleStart, config.ScheduleEnd),
        _                      => new AlwaysAllowPolicy()
    };
}
```

- [ ] **Step 4: Ejecutar — deben PASAR los 10 tests del archivo**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyFactoryTests" -v minimal
```
Esperado: 10 tests pasan.

- [ ] **Step 5: Commit**

```
git add src/Application/Policies/AccessPolicyFactory.cs tests/SmartParkingLot.Tests/Application/AccessPolicyFactoryTests.cs
git commit -m "feat(application): AccessPolicyFactory — construye políticas dinámicamente"
```

---

### Task 5: EF — persistencia de `AccessPolicyConfig`

**Files:**
- Modify: `src/Infrastructure/Data/ParkingLotDbContext.cs`
- Create: `src/Infrastructure/Repositories/EFAccessPolicyConfigRepository.cs`

- [ ] **Step 1: Añadir `DbSet` y configuración EF al contexto**

Añadir en `ParkingLotDbContext.cs` el using y el DbSet:

```csharp
// Al inicio del archivo, añadir el using:
using System.Text.Json;
using SmartParkingLot.Core.Entities;
```

Añadir la propiedad dentro de la clase:
```csharp
public DbSet<AccessPolicyConfig> AccessPolicyConfigs { get; set; }
```

Añadir dentro de `OnModelCreating`, después de la configuración de `AlertLog`:
```csharp
modelBuilder.Entity<AccessPolicyConfig>(entity =>
{
    entity.HasKey(e => e.LotId);
    entity.Property(e => e.ScheduleStart)
          .HasConversion(v => v.ToString(), v => TimeSpan.Parse(v));
    entity.Property(e => e.ScheduleEnd)
          .HasConversion(v => v.ToString(), v => TimeSpan.Parse(v));
    entity.Property<List<string>>("_plates")
          .HasColumnName("AllowedPlatesJson")
          .HasConversion(
              v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
              v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
});
```

- [ ] **Step 2: Compilar el proyecto Infrastructure**

```
dotnet build src/Infrastructure/SmartParkingLot.Infrastructure.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Generar la migración EF**

```
dotnet ef migrations add AddAccessPolicyConfig --project src/Infrastructure/SmartParkingLot.Infrastructure.csproj --startup-project src/Cli/SmartParkingLot.Cli.csproj
```
Esperado: se crea `src/Infrastructure/Migrations/<timestamp>_AddAccessPolicyConfig.cs` con la tabla `AccessPolicyConfigs`.

- [ ] **Step 4: Crear el repositorio EF**

```csharp
// src/Infrastructure/Repositories/EFAccessPolicyConfigRepository.cs
using Microsoft.EntityFrameworkCore;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Infrastructure.Data;

namespace SmartParkingLot.Infrastructure.Repositories;

public sealed class EFAccessPolicyConfigRepository : IAccessPolicyConfigRepository
{
    private readonly ParkingLotDbContext _context;

    public EFAccessPolicyConfigRepository(ParkingLotDbContext context)
    {
        _context = context;
    }

    public async Task<AccessPolicyConfig?> GetByLotIdAsync(string lotId, CancellationToken ct = default)
        => await _context.AccessPolicyConfigs
               .FirstOrDefaultAsync(c => c.LotId == lotId, ct);

    public async Task SaveAsync(AccessPolicyConfig config, CancellationToken ct = default)
    {
        var existing = await _context.AccessPolicyConfigs
            .FirstOrDefaultAsync(c => c.LotId == config.LotId, ct);

        if (existing is null)
            _context.AccessPolicyConfigs.Add(config);
        else
            _context.Entry(existing).CurrentValues.SetValues(config);

        await _context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Compilar Infrastructure completo**

```
dotnet build src/Infrastructure/SmartParkingLot.Infrastructure.csproj
```
Esperado: Build succeeded.

- [ ] **Step 6: Commit**

```
git add src/Infrastructure/Data/ParkingLotDbContext.cs src/Infrastructure/Repositories/EFAccessPolicyConfigRepository.cs src/Infrastructure/Migrations/
git commit -m "feat(infra): persistencia EF para AccessPolicyConfig (tabla AccessPolicyConfigs)"
```

---

### Task 6: `IAccessPolicyConfigService` y su implementación

**Files:**
- Create: `src/Core/Interfaces/IAccessPolicyConfigService.cs`
- Create: `src/Application/Services/AccessPolicyConfigService.cs`
- Create: `tests/SmartParkingLot.Tests/Application/AccessPolicyConfigServiceTests.cs`

- [ ] **Step 1: Crear la interfaz**

```csharp
// src/Core/Interfaces/IAccessPolicyConfigService.cs
using SmartParkingLot.Core.Entities;

namespace SmartParkingLot.Core.Interfaces;

public interface IAccessPolicyConfigService
{
    Task<AccessPolicyConfig> GetAsync(string lotId, CancellationToken ct = default);
    Task UpdateWhitelistAsync(string lotId, IEnumerable<string> plates, CancellationToken ct = default);
    Task UpdateScheduleAsync(string lotId, TimeSpan start, TimeSpan end, CancellationToken ct = default);
}
```

- [ ] **Step 2: Escribir los tests antes de la implementación**

```csharp
// tests/SmartParkingLot.Tests/Application/AccessPolicyConfigServiceTests.cs
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Application.Services;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Tests.Application;

public class AccessPolicyConfigServiceTests
{
    private const string LOT_ID = "LOT-1";

    [Fact]
    public async Task GetAsync_returns_existing_config()
    {
        var (svc, repo) = Build();
        var config = new AccessPolicyConfig(LOT_ID);
        config.SetWhitelist(["ABC-123"]);
        await repo.SaveAsync(config);

        var result = await svc.GetAsync(LOT_ID);

        Assert.Contains("ABC-123", result.AllowedPlates);
    }

    [Fact]
    public async Task GetAsync_creates_default_config_when_missing()
    {
        var (svc, _) = Build();
        var result = await svc.GetAsync(LOT_ID);
        Assert.NotNull(result);
        Assert.Equal(LOT_ID, result.LotId);
    }

    [Fact]
    public async Task UpdateWhitelistAsync_persists_and_reapplies_policy()
    {
        var (svc, repo) = Build();

        await svc.UpdateWhitelistAsync(LOT_ID, ["XYZ-999"]);

        var saved = await repo.GetByLotIdAsync(LOT_ID);
        Assert.NotNull(saved);
        Assert.Contains("XYZ-999", saved!.AllowedPlates);
    }

    [Fact]
    public async Task UpdateScheduleAsync_persists_and_reapplies_policy()
    {
        var (svc, repo) = Build();
        var start = TimeSpan.FromHours(9);
        var end   = TimeSpan.FromHours(18);

        await svc.UpdateScheduleAsync(LOT_ID, start, end);

        var saved = await repo.GetByLotIdAsync(LOT_ID);
        Assert.NotNull(saved);
        Assert.Equal(start, saved!.ScheduleStart);
        Assert.Equal(end,   saved!.ScheduleEnd);
    }

    private static (AccessPolicyConfigService svc, InMemoryConfigRepo repo) Build()
    {
        var lot        = new ParkingLot(LOT_ID, "Test");
        var queue      = new InMemoryApprovalQueue();
        var logger     = new NullLogger();
        var factory    = new AccessPolicyFactory(queue, logger, TimeSpan.FromSeconds(30));
        var switchable = new SwitchableAccessPolicy(new AlwaysAllowPolicy());
        var modeRepo   = new InMemoryLotRepo(lot);
        var repo       = new InMemoryConfigRepo();
        var modeSvc    = new ParkingModeService(lot, switchable, modeRepo, logger, factory, repo);
        var svc        = new AccessPolicyConfigService(repo, modeSvc, lot);
        return (svc, repo);
    }

    private sealed class NullLogger : ILogger
    {
        public void Debug(string s, string m) { }
        public void Info(string s, string m)  { }
        public void Warn(string s, string m)  { }
        public void Error(string s, string m) { }
    }

    private sealed class InMemoryConfigRepo : IAccessPolicyConfigRepository
    {
        private readonly Dictionary<string, AccessPolicyConfig> _store = new();
        public Task<AccessPolicyConfig?> GetByLotIdAsync(string lotId, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(lotId, out var c) ? c : null);
        public Task SaveAsync(AccessPolicyConfig config, CancellationToken ct = default)
        { _store[config.LotId] = config; return Task.CompletedTask; }
    }

    private sealed class InMemoryLotRepo : IParkingLotRepository
    {
        private readonly ParkingLot _lot;
        public InMemoryLotRepo(ParkingLot lot) { _lot = lot; }
        public Task<ParkingLot?> GetParkingLotByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult<ParkingLot?>(_lot);
        public Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<ParkingSpot>>([]);
        public Task<bool> UpdateLotModeAsync(string id, ParkingMode mode, CancellationToken ct = default)
        { _lot.SetMode(mode); return Task.FromResult(true); }
    }
}
```

- [ ] **Step 3: Ejecutar — debe FALLAR (AccessPolicyConfigService no existe)**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyConfigServiceTests" -v minimal
```
Esperado: error de compilación.

- [ ] **Step 4: Crear `AccessPolicyConfigService`**

```csharp
// src/Application/Services/AccessPolicyConfigService.cs
using SmartParkingLot.Core;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Services;

public sealed class AccessPolicyConfigService : IAccessPolicyConfigService
{
    private readonly IAccessPolicyConfigRepository _repo;
    private readonly IParkingModeService           _modeService;
    private readonly ParkingLot                    _lot;

    public AccessPolicyConfigService(
        IAccessPolicyConfigRepository repo,
        IParkingModeService modeService,
        ParkingLot lot)
    {
        _repo        = repo;
        _modeService = modeService;
        _lot         = lot;
    }

    public async Task<AccessPolicyConfig> GetAsync(string lotId, CancellationToken ct = default)
    {
        var config = await _repo.GetByLotIdAsync(lotId, ct);
        if (config is not null) return config;

        var defaultConfig = new AccessPolicyConfig(lotId);
        await _repo.SaveAsync(defaultConfig, ct);
        return defaultConfig;
    }

    public async Task UpdateWhitelistAsync(string lotId, IEnumerable<string> plates, CancellationToken ct = default)
    {
        var config = await GetAsync(lotId, ct);
        config.SetWhitelist(plates);
        await _repo.SaveAsync(config, ct);
        await _modeService.SwitchToAsync(_lot.Mode);
    }

    public async Task UpdateScheduleAsync(string lotId, TimeSpan start, TimeSpan end, CancellationToken ct = default)
    {
        var config = await GetAsync(lotId, ct);
        config.SetSchedule(start, end);
        await _repo.SaveAsync(config, ct);
        await _modeService.SwitchToAsync(_lot.Mode);
    }
}
```

- [ ] **Step 5: Ejecutar — debe FALLAR porque `ParkingModeService` aún usa el Func inline**

```
dotnet test tests/SmartParkingLot.Tests --filter "AccessPolicyConfigServiceTests" -v minimal
```
Los tests de servicio fallarán hasta que actualicemos `ParkingModeService` en el Task 7.

- [ ] **Step 6: Commit parcial**

```
git add src/Core/Interfaces/IAccessPolicyConfigService.cs src/Application/Services/AccessPolicyConfigService.cs tests/SmartParkingLot.Tests/Application/AccessPolicyConfigServiceTests.cs
git commit -m "feat(application): AccessPolicyConfigService — gestiona config de políticas en caliente"
```

---

### Task 7: Actualizar `ParkingModeService` para usar `IAccessPolicyFactory`

**Files:**
- Modify: `src/Application/Services/ParkingModeService.cs`

- [ ] **Step 1: Reemplazar la firma y el cuerpo de `ParkingModeService`**

```csharp
// src/Application/Services/ParkingModeService.cs
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Services;

public sealed class ParkingModeService : IParkingModeService
{
    private const string LogSource = "ParkingModeService";

    private readonly ParkingLot                     _lot;
    private readonly SwitchableAccessPolicy         _switchable;
    private readonly IParkingLotRepository          _repository;
    private readonly ILogger                        _logger;
    private readonly IAccessPolicyFactory           _factory;
    private readonly IAccessPolicyConfigRepository  _configRepo;

    public ParkingMode Current => _lot.Mode;

    public ParkingModeService(
        ParkingLot lot,
        SwitchableAccessPolicy switchable,
        IParkingLotRepository repository,
        ILogger logger,
        IAccessPolicyFactory factory,
        IAccessPolicyConfigRepository configRepo)
    {
        _lot        = lot;
        _switchable = switchable;
        _repository = repository;
        _logger     = logger;
        _factory    = factory;
        _configRepo = configRepo;
    }

    public async Task SwitchToAsync(ParkingMode mode)
    {
        var config = await _configRepo.GetByLotIdAsync(_lot.Id)
                     ?? new Core.Entities.AccessPolicyConfig(_lot.Id);

        _switchable.Set(_factory.Create(mode, config));
        _lot.SetMode(mode);
        await _repository.UpdateLotModeAsync(_lot.Id, mode).ConfigureAwait(false);
        _logger.Info(LogSource, $"Modo cambiado a {mode}");
    }
}
```

- [ ] **Step 2: Ejecutar todos los tests del proyecto**

```
dotnet test tests/SmartParkingLot.Tests -v minimal
```
Esperado: todos pasan (los de `AccessPolicyConfigServiceTests` ahora también).

- [ ] **Step 3: Commit**

```
git add src/Application/Services/ParkingModeService.cs
git commit -m "refactor(application): ParkingModeService delega construcción de política a IAccessPolicyFactory"
```

---

### Task 8: Actualizar el registro de DI en `ApplicationModule.cs`

**Files:**
- Modify: `src/Application/Bootstrap/ApplicationModule.cs`

- [ ] **Step 1: Reemplazar los registros inline de `SwitchableAccessPolicy` e `IParkingModeService`**

Localizar las líneas que registran `SwitchableAccessPolicy` e `IParkingModeService` (aprox. líneas 140–166) y reemplazarlas por:

```csharp
var manualTimeout = TimeSpan.FromSeconds(hwConfig.ManualApprovalTimeoutSeconds);

services.AddSingleton<IAccessPolicyConfigRepository>(sp =>
    new EFAccessPolicyConfigRepository(sp.GetRequiredService<ParkingLotDbContext>()));

services.AddSingleton<IAccessPolicyFactory>(sp =>
    new AccessPolicyFactory(
        sp.GetRequiredService<IApprovalQueue>(),
        sp.GetRequiredService<ILogger>(),
        manualTimeout));

services.AddSingleton<SwitchableAccessPolicy>(sp =>
{
    var factory    = sp.GetRequiredService<IAccessPolicyFactory>();
    var configRepo = sp.GetRequiredService<IAccessPolicyConfigRepository>();
    var config     = configRepo.GetByLotIdAsync(lot.Id).GetAwaiter().GetResult()
                     ?? new AccessPolicyConfig(lot.Id);
    return new SwitchableAccessPolicy(factory.Create(lot.Mode, config));
});
services.AddSingleton<IAccessPolicy>(sp => sp.GetRequiredService<SwitchableAccessPolicy>());

services.AddSingleton<IParkingModeService>(sp =>
    new ParkingModeService(
        lot,
        sp.GetRequiredService<SwitchableAccessPolicy>(),
        repository,
        sp.GetRequiredService<ILogger>(),
        sp.GetRequiredService<IAccessPolicyFactory>(),
        sp.GetRequiredService<IAccessPolicyConfigRepository>()));

services.AddSingleton<IAccessPolicyConfigService>(sp =>
    new AccessPolicyConfigService(
        sp.GetRequiredService<IAccessPolicyConfigRepository>(),
        sp.GetRequiredService<IParkingModeService>(),
        lot));
```

Añadir los usings necesarios al inicio de `ApplicationModule.cs`:
```csharp
using SmartParkingLot.Application.Policies;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Infrastructure.Repositories;
```

- [ ] **Step 2: Compilar la solución completa**

```
dotnet build
```
Esperado: Build succeeded, 0 errores.

- [ ] **Step 3: Ejecutar todos los tests**

```
dotnet test tests/SmartParkingLot.Tests -v minimal
```
Esperado: todos pasan.

- [ ] **Step 4: Commit**

```
git add src/Application/Bootstrap/ApplicationModule.cs
git commit -m "feat(di): cablear AccessPolicyFactory, IAccessPolicyConfigRepository e IAccessPolicyConfigService en DI"
```

---

### Task 9: Arrancar la app y verificar F1

- [ ] **Step 1: Arrancar la app GUI**

```
dotnet run --project src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: la app arranca sin errores. La migración `AddAccessPolicyConfig` se aplica automáticamente al inicializar la BD (el `DatabaseInitializer` llama `MigrateAsync`).

- [ ] **Step 2: Verificar en SQLite que la tabla existe**

```
sqlite3 src/GUI/bin/Debug/net10.0-windows10.0.19041.0/win-x64/data/smartparking.db ".tables"
```
Esperado: aparece `AccessPolicyConfigs` en la lista.

- [ ] **Step 3: Commit final de F1**

```
git add -A
git commit -m "feat(f1): backend de políticas de acceso completo — 4 modos funcionales"
```
