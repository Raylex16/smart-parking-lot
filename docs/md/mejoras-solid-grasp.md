# Mejoras SOLID y GRASP — Próxima Entrega

Análisis de problemas identificados en la iteración actual, ordenados por impacto.

---

## Problemas de alta prioridad

### 1. LSP — `NotImplementedException` en métodos de la interfaz

**Archivo:** `src/Core/Interfaces/IParkingRepository.cs`

```csharp
public Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(...)
    => throw new NotImplementedException(); // viola LSP

public Task<IEnumerable<ParkingSpot>> GetOccupiedSpotsAsync(...)
    => throw new NotImplementedException(); // viola LSP
```

**Problema:** No se puede sustituir `IParkingRepository` en todos los contextos — crashea en runtime si algún consumidor llama esos métodos.

**Solución:** Implementar los métodos en `SqliteParkingRepository`, o eliminarlos de la interfaz si no se usan.

```csharp
// SqliteParkingRepository.cs
public async Task<IEnumerable<ParkingSpot>> GetAvailableSpotsAsync(string lotId, CancellationToken ct = default)
{
    // SELECT ... WHERE IsOccupied = 0
}
```

---

## Problemas de prioridad media

### 2. ISP + SRP — `IParkingRepository` es una interfaz gorda

**Archivo:** `src/Core/Interfaces/IParkingRepository.cs`

**Problema:** Una sola interfaz cubre seis responsabilidades distintas: lotes, spots, logs de requests, lecturas de sensores, acciones de dispositivos y alertas. `SqliteParkingRepository` hereda el mismo problema.

**Solución:** Segregar en interfaces especializadas:

```csharp
public interface ISpotRepository
{
    Task<ParkingLot?> GetParkingLotByIdAsync(string lotId, CancellationToken ct = default);
    Task<IEnumerable<ParkingSpot>> GetSpotsByLotIdAsync(string lotId, CancellationToken ct = default);
    Task<bool> UpdateSpotStatusAsync(string spotId, bool isOccupied, CancellationToken ct = default);
    Task EnsureSpotExistsAsync(string spotId, string lotId, string address, string type, string floor, CancellationToken ct = default);
}

public interface IAuditRepository
{
    Task<bool> LogRequestAsync(...);
    Task<IEnumerable<...>> GetRequestHistoryAsync(...);
}

public interface ISensorRepository
{
    Task<bool> LogSensorReadingAsync(...);
    Task<IEnumerable<...>> GetSensorReadingsAsync(...);
}

public interface IDeviceRepository
{
    Task<bool> LogDeviceActionAsync(...);
    Task<IEnumerable<...>> GetDeviceActionsAsync(...);
}
```

Cada consumidor dependería solo de la interfaz que necesita (Low Coupling).

---

### 3. SRP + High Cohesion — `ConsoleMenu` hace demasiado

**Archivo:** `src/Cli/ConsoleMenu.cs`

**Problema:** 300+ líneas que mezclan responsabilidades:
- Renderizado del menú
- Orquestación de entradas y salidas de vehículos
- Persistencia directa al repositorio
- Lecturas manuales de sensores
- Queries de historial y auditoría
- Monitoreo serial en tiempo real

Además, algunas opciones (entrada/salida) persisten directamente sin pasar por un use case, inconsistente con cómo se manejan las lecturas de sensores (vía bus de eventos).

**Solución:** Extraer cada opción a su propio use case en `src/Application/UseCases/`:

```
HandleVehicleEntryUseCase.cs
HandleVehicleExitUseCase.cs
GetParkingStatusUseCase.cs
GetVehicleHistoryUseCase.cs
```

`ConsoleMenu` quedaría como un controlador delgado que solo recibe input, delega al use case correspondiente y muestra el resultado.

---

## Problemas de prioridad baja

### 4. DIP — `ConsoleMenu` depende de `SerialCommandDispatcher` concreto

**Archivo:** `src/Cli/ConsoleMenu.cs`

```csharp
private readonly SerialCommandDispatcher _dispatcher; // clase concreta
```

**Problema:** La capa de UI queda acoplada a la infraestructura de hardware. Dificulta pruebas unitarias del menú.

**Solución:** Extraer una interfaz con la capacidad de logging:

```csharp
// src/Core/Interfaces/ILoggable.cs
public interface ILoggable
{
    bool ConsoleLoggingEnabled { get; set; }
}
```

`SerialCommandDispatcher` implementaría `ILoggable`, y `ConsoleMenu` recibiría `ILoggable` en lugar de la clase concreta.

---

### 5. Information Expert — Ambigüedad en `ParkingSpot`

**Archivo:** `src/Core/Entities/ParkingSpot.cs`

```csharp
public void Occupy()  { if (IsOccupied) throw ...; IsOccupied = true;  }
public void Release() { if (!IsOccupied) throw ...; IsOccupied = false; }
public void ApplyOccupancy(bool isOccupied, string source) { ... } // idempotente
```

**Problema:** Tres métodos para cambiar el mismo estado con contratos distintos. `Occupy` y `Release` lanzan excepciones; `ApplyOccupancy` es idempotente. Solo `ApplyOccupancy` se usa en el flujo principal; `Occupy`/`Release` solo aparecen en la reconstrucción del repositorio.

**Solución:** Eliminar `Occupy` y `Release`. Usar `ApplyOccupancy` en todos los contextos, incluyendo la reconstrucción:

```csharp
// SqliteParkingRepository.cs — reconstrucción
if (spotDto.IsOccupied == 1)
    spot.ApplyOccupancy(true, "persistence:restore");
```

---

### 6. Performance — Búsqueda lineal en `HandleSensorReadingUseCase`

**Archivo:** `src/Application/UseCases/HandleSensorReadingUseCase.cs`

```csharp
var spot = _lot.GetSpots().FirstOrDefault(s => s.Id == spotId); // O(n) por cada lectura
```

**Problema:** Con pocos spots es irrelevante, pero escala mal.

**Solución:** `ParkingLot` podría exponer un indexador interno:

```csharp
// ParkingLot.cs
private readonly Dictionary<string, ParkingSpot> _spotsById;

public ParkingSpot? GetSpotById(string id) =>
    _spotsById.GetValueOrDefault(id);
```

---

## Resumen

| # | Problema | Principio | Prioridad |
|---|---|---|---|
| 1 | `NotImplementedException` en interfaz | LSP | Alta |
| 2 | `IParkingRepository` con 10+ métodos | ISP / SRP | Media |
| 3 | `ConsoleMenu` con 6 responsabilidades | SRP / High Cohesion | Media |
| 4 | `SerialCommandDispatcher` concreto en UI | DIP | Baja |
| 5 | `Occupy`/`Release` vs `ApplyOccupancy` | Information Expert | Baja |
| 6 | Búsqueda O(n) por lectura de sensor | — | Baja |
