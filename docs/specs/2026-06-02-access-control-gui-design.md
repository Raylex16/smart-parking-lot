# Diseño: Control de Acceso — GUI + Backend

**Fecha:** 2026-06-02  
**Rama base:** `gui-fixes-arduino`  
**Alcance:** 3 fases entregables de forma independiente

---

## Contexto y motivación

La GUI no tiene ningún punto de entrada para cambiar el modo de operación del parqueadero ni para gestionar las políticas de acceso. El backend ya tiene todo el andamiaje (`IParkingModeService`, `SwitchableAccessPolicy`, `IApprovalQueue`, cuatro políticas), pero nada de eso está cableado a la interfaz.

Adicionalmente, la página "Gestión de Spots" es solo-lectura y redundante con el Mapa de Spots. Se reconvierte en "Control de Acceso" para darle propósito real.

El Historial (`LogPage`) funciona correctamente y no se modifica.

---

## Decisiones de diseño fijadas

| Decisión | Elección | Razón |
|----------|----------|-------|
| Alcance de políticas | Las 4 funcionales en esta tanda | Completitud operativa |
| Organización UI | Reconvertir `AdminPage` en "Control de Acceso" | Elimina redundancia, le da propósito a página vacía |
| Persistencia de config | SQLite (nueva tabla via EF) | Consistente con resto del dominio |
| Modelado de modo | Ampliar `ParkingMode` a 4 valores (Opción A) | Reutiliza persistencia y servicios existentes |

---

## Fase 1 — Núcleo de políticas (backend)

### 1.1 `ParkingMode` → 4 valores

```csharp
// Core/Enums/ParkingMode.cs
public enum ParkingMode { AUTOMATIC, MANUAL, RESTRICTED, SCHEDULED }
```

Impacto: migración EF para que la columna `Mode` en `ParkingLots` acepte los 4 valores como string. Los valores existentes `AUTOMATIC` / `MANUAL` no cambian en datos persistidos.

### 1.2 Entidad `AccessPolicyConfig` (`Core/Entities/`)

```csharp
public class AccessPolicyConfig
{
    public string LotId { get; }
    public List<string> AllowedPlates { get; private set; }   // JSON en SQLite
    public TimeSpan ScheduleStart { get; private set; }
    public TimeSpan ScheduleEnd { get; private set; }

    public void SetWhitelist(IEnumerable<string> plates) { ... }
    public void SetSchedule(TimeSpan start, TimeSpan end) { ... }
}
```

Una fila por lote. Se crea en el seed si no existe (con lista vacía y horario 08:00–20:00 por defecto). No es aggregate raíz independiente: es configuración del lote (Information Expert).

### 1.3 `IAccessPolicyConfigRepository` (`Core/Interfaces/`)

```csharp
Task<AccessPolicyConfig?> GetByLotIdAsync(string lotId, CancellationToken ct = default);
Task SaveAsync(AccessPolicyConfig config, CancellationToken ct = default);
```

Implementación EF en `Infrastructure/Repositories/EFAccessPolicyConfigRepository`. Migración: tabla `AccessPolicyConfigs` con columna `AllowedPlatesJson` (TEXT) y `ScheduleStart`/`ScheduleEnd` (TEXT como `hh:mm:ss`).

### 1.4 `AccessPolicyFactory` (`Application/Policies/`)

Reemplaza el `Func<ParkingMode, IAccessPolicy>` inline que hoy vive en `ApplicationModule.cs`. Se registra como servicio en DI.

```csharp
public interface IAccessPolicyFactory
{
    IAccessPolicy Create(ParkingMode mode, AccessPolicyConfig config);
}

public sealed class AccessPolicyFactory : IAccessPolicyFactory
{
    // Recibe IApprovalQueue, ILogger, TimeSpan manualTimeout por DI
    public IAccessPolicy Create(ParkingMode mode, AccessPolicyConfig config) => mode switch
    {
        ParkingMode.AUTOMATIC  => new AlwaysAllowPolicy(),
        ParkingMode.MANUAL     => new ManualAccessPolicy(_queue, _logger, _timeout),
        ParkingMode.RESTRICTED => new RestrictedAccessPolicy(config.AllowedPlates, _logger),
        ParkingMode.SCHEDULED  => new ScheduledBasedPolicy(config.ScheduleStart, config.ScheduleEnd),
        _                      => new AlwaysAllowPolicy()
    };
}
```

### 1.5 `IAccessPolicyConfigService` (`Application/Services/`)

Servicio de aplicación que expone la GUI. Encapsula persistencia + reaplicación de política en caliente.

```csharp
public interface IAccessPolicyConfigService
{
    Task<AccessPolicyConfig> GetAsync(string lotId, CancellationToken ct = default);
    Task UpdateWhitelistAsync(string lotId, IEnumerable<string> plates, CancellationToken ct = default);
    Task UpdateScheduleAsync(string lotId, TimeSpan start, TimeSpan end, CancellationToken ct = default);
}
```

Implementación `AccessPolicyConfigService`:
1. Persiste el cambio via `IAccessPolicyConfigRepository`.
2. Lee el `ParkingMode` actual del lote.
3. Llama `IParkingModeService.SwitchToAsync(modoActual)` para que `AccessPolicyFactory` reconstruya la política con los parámetros actualizados y la inyecte en `SwitchableAccessPolicy`.

### 1.6 Cambio en `ParkingModeService`

`SwitchToAsync` deja de usar el `Func<ParkingMode, IAccessPolicy>` inline. En su lugar:
1. Lee `AccessPolicyConfig` del repositorio vía `IAccessPolicyConfigRepository`.
2. Llama `IAccessPolicyFactory.Create(mode, config)`.
3. Entrega la nueva política a `SwitchableAccessPolicy.Set(...)`.

---

## Fase 2 — Página "Control de Acceso" (GUI)

### 2.1 Renombrado y navegación

| Elemento | Antes | Después |
|----------|-------|---------|
| Tag NavItem | `"admin"` | `"access"` |
| Texto NavItem | `"Gestión de Spots"` | `"Control de Acceso"` |
| Ícono | `ContactInfo` | `SymbolIcon Symbol="Permissions"` o `FontIcon` candado |
| ViewModel | `AdminPageViewModel` | `AccessControlViewModel` |
| Code-behind | `AdminPage.xaml.cs` | se reutiliza el archivo, se actualiza la referencia al VM |

El `AdminPageViewModel` y `SpotAdminRowVm` se eliminan. El código de spots/filtro no tiene nuevo hogar (el Mapa lo cubre).

### 2.2 Layout de `AdminPage.xaml`

Tres secciones verticales dentro de un `ScrollViewer`:

**A — Selector de política activa**
- `ComboBox` (o grupo `RadioButton`) con las 4 opciones: Automático, Manual, Restringido, Programado.
- Al cambiar → `AccessControlViewModel.SwitchPolicyCommand(ParkingMode)`.
- Badge de color junto al selector indicando la política en efecto (verde/naranja/rojo/azul).
- Se carga el modo actual en `Activate()` leyendo `IParkingModeService.Current`.

**B — Panel de parámetros** (Visibility condicional por modo)
- `AUTOMATIC` / `MANUAL`: panel oculto (`Collapsed`).
- `RESTRICTED`: 
  - Lista de placas permitidas (`ItemsRepeater` con botón eliminar por fila).
  - `TextBox` + botón "Añadir placa".
  - Botón "Guardar whitelist" → `SaveWhitelistCommand`.
- `SCHEDULED`:
  - Dos `TimePicker` (Inicio / Fin).
  - Botón "Guardar horario" → `SaveScheduleCommand`.

**C — Cola de aprobaciones** (siempre visible, detalle en Fase 3)

### 2.3 `AccessControlViewModel` (`GUI/ViewModels/`)

Dependencias:
- `IParkingModeService`
- `IAccessPolicyConfigService`
- `IApprovalQueue`
- `IApprovalDecisionService`
- `ILotSnapshotStream`
- `IUiThreadDispatcher`

Propiedades observables:
```
CurrentMode         : ParkingMode        (binding al selector)
PolicyBadgeText     : string             ("AUTOMÁTICO", etc.)
PolicyBadgeColor    : Brush
ParamsVisibility    : Visibility         (RESTRICTED o SCHEDULED)
WhitelistVisibility : Visibility
ScheduleVisibility  : Visibility
Plates              : ObservableCollection<PlateRowVm>
NewPlate            : string
ScheduleStart       : TimeSpan
ScheduleEnd         : TimeSpan
PendingApprovals    : ObservableCollection<PendingApprovalRowVm>
PendingBadgeText    : string             ("3 pendientes" / "Sin pendientes")
```

Comandos:
- `SwitchPolicyCommand(ParkingMode)`
- `AddPlateCommand` / `RemovePlateCommand(string)`
- `SaveWhitelistCommand`
- `SaveScheduleCommand`
- `ApproveCommand(string id)` / `DenyCommand(string id)`

---

## Fase 3 — Cola de aprobaciones en vivo (GUI)

### 3.1 Suscripción en tiempo real

`AccessControlViewModel.Activate()` suscribe a `IApprovalQueue.Enqueued`:
```csharp
_queue.Enqueued += approval => _ui.Enqueue(() => AddApprovalRow(approval));
```
`Deactivate()` desuscribe. Al activar también carga `_queue.GetPending()` para mostrar las ya encoladas.

### 3.2 `PendingApprovalRowVm`

```csharp
public partial class PendingApprovalRowVm : ObservableObject
{
    public string Id { get; }
    public string Plate { get; }
    public string GateId { get; }
    [ObservableProperty] private int _elapsedSeconds;
    public string ElapsedLabel => $"hace {ElapsedSeconds}s";
    // Timer de 1s que incrementa ElapsedSeconds hasta que se resuelve
    public IRelayCommand ApproveCommand { get; }
    public IRelayCommand DenyCommand { get; }
}
```

### 3.3 Layout de la sección de aprobaciones

```
┌─────────────────────────────────────────────────────┐
│  APROBACIONES PENDIENTES                 [3 pendientes]│
├───────────────┬──────────┬──────────┬───────────────┤
│  APR-3a7f1b2c │  ABC-123 │ GATE-G-01│ hace 8s  [✓][✗]│
│  APR-9c2d4e5f │  XYZ-456 │ GATE-G-01│ hace 22s [✓][✗]│
└───────────────┴──────────┴──────────┴───────────────┘
```

Al aprobar/denegar: `IApprovalDecisionService.Approve(id)` / `.Deny(id)` → la fila se retira de `PendingApprovals`.

Cuando el modo no es `MANUAL`, la sección muestra "Sin aprobaciones pendientes — el modo actual no requiere aprobación manual."

---

## Registro de DI

En `ServiceCollectionExtensions.cs` (GUI):
- Registrar `IAccessPolicyConfigService` → `AccessPolicyConfigService`
- Registrar `IAccessPolicyFactory` → `AccessPolicyFactory`
- Registrar `IAccessPolicyConfigRepository` → `EFAccessPolicyConfigRepository`
- Reemplazar registro de `AdminPageViewModel` → `AccessControlViewModel`

En `ApplicationModule.cs`:
- Retirar el `Func<ParkingMode, IAccessPolicy>` inline de los registros de `SwitchableAccessPolicy` y `IParkingModeService`.
- Resolver `IAccessPolicyFactory` desde el container en ambos registros.

---

## Migración EF

Nueva migración `AddAccessPolicyConfig`:
```
Tabla AccessPolicyConfigs:
  - LotId              TEXT NOT NULL (PK, FK → ParkingLots.Id)
  - AllowedPlatesJson  TEXT NOT NULL DEFAULT '[]'
  - ScheduleStart      TEXT NOT NULL DEFAULT '08:00:00'
  - ScheduleEnd        TEXT NOT NULL DEFAULT '20:00:00'
```

Seed: insertar fila para el lote existente si no hay ninguna.

---

## Invariantes y límites

- `RESTRICTED` con whitelist vacía → todos denegados. La UI advierte con un texto de ayuda: "La whitelist está vacía: ningún vehículo podrá ingresar."
- `SCHEDULED` con `ScheduleEnd ≤ ScheduleStart` → se valida en el VM antes de guardar, se muestra error inline.
- Cambiar de modo mientras hay aprobaciones `MANUAL` pendientes → se muestran hasta expirar (no se cancelan de forma forzada; el timeout de `ManualAccessPolicy` ya las deniega solas).
- El `SwitchableAccessPolicy` ya es thread-safe (field assignment atómico en .NET).

---

## Lo que no cambia

- `GateController` — sigue dependiendo de `IAccessPolicy` (Low Coupling intacto).
- `IApprovalQueue`, `InMemoryApprovalQueue` — sin modificaciones.
- `ManualAccessPolicy`, `AlwaysAllowPolicy`, `RestrictedAccessPolicy`, `ScheduledBasedPolicy` — sin modificaciones.
- `LogPage` — sin modificaciones.
- `MapPage` — sin modificaciones (sigue siendo el lugar para ver y alternar spots).

---

## Fases y verificación

| Fase | Qué se verifica | Cómo |
|------|----------------|------|
| F1 | Factory construye la política correcta con config; repo persiste y recupera; `SwitchToAsync` usa factory | Tests unitarios de `AccessPolicyFactory` + tests de repositorio con SQLite in-memory |
| F2 | GUI muestra modo actual, cambia de política, persiste whitelist/horario | App corriendo: cambiar a RESTRICTED, añadir placa, reiniciar → placa sigue en lista |
| F3 | Modo MANUAL + solicitud de entrada → aparece en cola → aprobar/denegar desde GUI → barrera reacciona | Prueba manual end-to-end con simulación de IR en Hardware page |
