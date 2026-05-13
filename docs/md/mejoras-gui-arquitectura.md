# Mejoras Arquitectónicas y de Diseño en la Interfaz Gráfica (GUI)

Este documento detalla las mejoras arquitectónicas identificadas para el proyecto de interfaz gráfica ([src/GUI/](src/GUI/)), desarrollado con **WinUI 3** sobre **.NET 10**. Aunque el núcleo de dominio (`SmartParkingLot.Core` y `SmartParkingLot.Application`) está bien estructurado y publica eventos limpios, la integración en la capa de UI presenta oportunidades importantes de refactorización hacia patrones más modernos, mantenibles y testeables.

Cada mejora se acompaña de **evidencia** (referencias concretas a archivos y líneas del código actual) y **acciones** (cambios mínimos para resolver el problema sin romper la composición existente).

---

## 1. Implementación del Patrón MVVM (Model-View-ViewModel)

**Problema actual.** La UI depende fuertemente del patrón **Code-Behind**. La actualización de controles se realiza manualmente desde los `.xaml.cs`. Por ejemplo, [DashboardPage.xaml.cs:47-58](src/GUI/Pages/DashboardPage.xaml.cs#L47-L58) asigna cada propiedad de cada tile (`TileOccupied.Text = occ.ToString();`, `TilePct.Text = $"{pct}%";`, …) en lugar de enlazar las vistas a un estado observable.

**Mejora.** Adoptar **MVVM** aprovechando que WinUI 3 está diseñado para `{x:Bind}`:

- Crear un `DashboardViewModel` con propiedades `Occupied`, `Available`, `OccupancyPercent`, `ZoneSummaries`, `Alerts`, `Gates`, que implementen `INotifyPropertyChanged` (o usar `CommunityToolkit.Mvvm` con `[ObservableProperty]` y `[RelayCommand]` para reducir el boilerplate).
- Reemplazar las asignaciones manuales por `{x:Bind ViewModel.Occupied, Mode=OneWay}`.
- Mantener el code-behind únicamente para responsabilidades visuales (animaciones, foco, gestos).

**Beneficios concretos:**
- Permite **pruebas unitarias** sobre la lógica de presentación sin necesidad de Dispatcher ni Window.
- Elimina llamadas como `_ui.TryEnqueue(Refresh)` (ver §6) porque el motor de binding se encarga del marshalling al hilo de UI cuando la propiedad notifica.
- Reduce la superficie del XAML *code-behind* a métodos triviales.

---

## 2. Respeto a las Capas Limpias (Separación UI ↔ Dominio/Persistencia)

**Problema actual.** Existe acoplamiento directo entre la interfaz y las capas subyacentes:

- [DashboardPage.xaml.cs:35](src/GUI/Pages/DashboardPage.xaml.cs#L35): `await _svc.Repository.GetSpotsByLotIdAsync(_svc.Lot.Id);` — la página consume el `IParkingRepository` directamente.
- [AdminPage.xaml.cs:28](src/GUI/Pages/AdminPage.xaml.cs#L28): idem.
- Las páginas operan sobre `ParkingSpot` (entidad de dominio) en lugar de un DTO/proyección de lectura ([DashboardPage.xaml.cs:61](src/GUI/Pages/DashboardPage.xaml.cs#L61), [AdminPage.xaml.cs:54](src/GUI/Pages/AdminPage.xaml.cs#L54)).
- La vista también accede directamente al `GateController` del dominio ([DashboardPage.xaml.cs:181](src/GUI/Pages/DashboardPage.xaml.cs#L181)).

**Mejora.** La UI no debe interactuar directamente con repositorios ni con entidades de dominio puro. Todas las interacciones deben canalizarse a través de la capa de **Application** (Casos de uso, Handlers o un mediador tipo `Bus`):

- Definir **casos de uso de lectura** (queries) como `GetLotSnapshotQuery`, `GetSpotsForAdminQuery`, que devuelvan **DTOs inmutables** (`LotSnapshotDto`, `SpotRowDto`).
- Inyectar esos casos de uso en los ViewModels en lugar del `Repository`.
- Las entidades de dominio dejan de cruzar la frontera de la vista; cualquier cambio de modelo no se propaga a XAML.

---

## 3. Eliminación del Anti-patrón "God Object" (Inyección de Dependencias)

**Problema actual.** El objeto `ParkingServices` ([src/GUI/Bootstrap/ParkingServices.cs](src/GUI/Bootstrap/ParkingServices.cs)) expone **catorce** propiedades públicas (`Lot`, `GateController`, `CapacityService`, `AlertService`, `Repository`, `Bus`, `SpotSensors`, `GateSensor`, `Bridge`, `Dispatcher`, `UiLogger`, `FileLogger`, `Config`, …). Se inyecta tal cual en cada página vía constructor (`new DashboardPage(_services)` en [MainWindow.xaml.cs:77-81](src/GUI/MainWindow.xaml.cs#L77-L81)), lo que viola **Interface Segregation** y obliga a cada vista a depender del grafo completo.

**Mejora.** Adoptar el contenedor estándar de .NET (`Microsoft.Extensions.DependencyInjection`):

- En `App.xaml.cs`, registrar servicios en un `ServiceCollection` (`AddSingleton<ICapacityService, CapacityService>()`, `AddTransient<DashboardViewModel>()`, …).
- Cada **página o ViewModel** declara en su constructor **solo** lo que requiere (p. ej., `DashboardViewModel(IGetLotSnapshotQuery query, IHardwareStatus status)`).
- `ParkingServices` desaparece o queda reducido a un **bootstrapper** que ensambla el contenedor y se desecha; deja de ser un parámetro de los `Page`.

Beneficio extra: el `App` puede resolver `Page`s vía `serviceProvider.GetRequiredService<DashboardPage>()`, lo cual hace que la composición de las vistas también sea testeable.

---

## 4. Desacoplamiento del Hardware Específico

**Problema actual.** La UI contiene referencias explícitas a hardware concreto:

- [MainWindow.xaml.cs:99-104](src/GUI/MainWindow.xaml.cs#L99-L104): `_services.Bridge.IsListening` y la cadena literal `"Arduino — desconectado"` están cableadas en la vista.
- `ArduinoSerialBridge` se expone como propiedad pública de `ParkingServices` y la vista lo consulta directamente.

**Mejora.** Abstraer el hardware detrás de un contrato propio de la capa Application:

```csharp
public interface IHardwareStatus
{
    bool IsConnected { get; }
    string DisplayName { get; }     // "Arduino COM3", "Mock", "MQTT broker", …
    event EventHandler? Changed;
}
```

La vista consume `IHardwareStatus` sin saber que detrás hay un puente serie. Si mañana se sustituye por MQTT o por un mock para demos, **el XAML no cambia**.

---

## 5. Fuga de Eventos de Dominio hacia la Capa de Vista *(nuevo)*

**Problema actual.** Las páginas se suscriben directamente a eventos de **entidades de dominio**:

- [DashboardPage.xaml.cs:26-27](src/GUI/Pages/DashboardPage.xaml.cs#L26-L27): `spot.OccupancyChanged += _ => _ui.TryEnqueue(Refresh);`
- [MainWindow.xaml.cs:60-61](src/GUI/MainWindow.xaml.cs#L60-L61): idem.

Esto significa que (a) el dominio queda atado al hilo de UI a través del cierre, y (b) **nunca se desuscribe** cuando la página se descarga, dejando referencias vivas que prolongan el ciclo de vida del `Page` y producen *memory leaks* y refrescos fantasma al navegar de regreso.

**Mejora.**

- Exponer una **proyección observable** desde Application (p. ej., `IObservable<LotSnapshotDto>` con `System.Reactive`, o un `IAsyncEnumerable<LotSnapshotDto>` que la UI consuma desde el ViewModel).
- Alternativamente, un **mensajero** (`IMessenger` del CommunityToolkit) al que la UI se suscribe con un *weak reference* y se desuscribe en `OnNavigatedFrom`.
- En cualquier caso, **toda suscripción que se cree en `Loaded` u `OnNavigatedTo` debe cancelarse en `Unloaded`/`OnNavigatedFrom`**, idealmente mediante un `CompositeDisposable` o `CancellationTokenSource` por página.

---

## 6. Marshalling Manual al Hilo de UI *(nuevo)*

**Problema actual.** El código usa `DispatcherQueue.TryEnqueue` repetidamente en lugares dispersos: [DashboardPage.xaml.cs:23](src/GUI/Pages/DashboardPage.xaml.cs#L23), [MainWindow.xaml.cs:24](src/GUI/MainWindow.xaml.cs#L24), [MainWindow.xaml.cs:61](src/GUI/MainWindow.xaml.cs#L61), [MainWindow.xaml.cs:107](src/GUI/MainWindow.xaml.cs#L107). Cada página guarda su propio `_ui` y debe acordarse de envolver cada callback.

**Mejora.**

- Si se adopta MVVM (§1), el motor de binding hace el marshalling automáticamente cuando una propiedad notifica desde otro hilo.
- Donde aún haga falta, encapsular el dispatcher en un servicio (`IUiThreadDispatcher`) inyectable y testeable con un *immediate dispatcher* en unit-tests.
- Eliminar las llamadas dispersas a `DispatcherQueue.GetForCurrentThread()`.

---

## 7. Construcción de Visuales en Code-Behind *(nuevo)*

**Problema actual.** Páginas enteras construyen `Grid`, `Border`, `StackPanel`, `TextBlock` y `FontIcon` **programáticamente** en C# en lugar de declararlos en XAML:

- [DashboardPage.xaml.cs:71-104](src/GUI/Pages/DashboardPage.xaml.cs#L71-L104) construye cada fila de zona.
- [DashboardPage.xaml.cs:136-173](src/GUI/Pages/DashboardPage.xaml.cs#L136-L173) construye cada alerta.
- [DashboardPage.xaml.cs:184-217](src/GUI/Pages/DashboardPage.xaml.cs#L184-L217) construye cada tarjeta de puerta.
- [AdminPage.xaml.cs:54-…](src/GUI/Pages/AdminPage.xaml.cs#L54) construye cada fila de la tabla.

Esto trae varios problemas: (a) imposible de previsualizar en el diseñador XAML, (b) hot-reload no funciona, (c) cualquier cambio de estilo obliga a recompilar, (d) se acumulan asignaciones manuales de pinceles vía `XamlApp.Current.Resources["…"]` (ver §10), (e) hace muy difícil aplicar virtualización.

**Mejora.**

- Reemplazar cada panel por un `ItemsControl` (o `ItemsRepeater` para colecciones grandes) enlazado a una `ObservableCollection<TViewModel>` con un `DataTemplate` definido en XAML.
- Llevar el estilo de tile/badge/row a `ResourceDictionary` (`Styles/Tile.xaml`, `Styles/Row.xaml`) — ya hay un `Styles/Theme.xaml`, conviene aprovecharlo.
- Para `Badge`, considerar promoverlo a `TemplatedControl` con `ControlTemplate` para que sea reutilizable y estilable por tema.

---

## 8. Riesgo de Memory Leaks por Navegación *(nuevo)*

**Problema actual.** Cada vez que el usuario navega al `Dashboard`, [MainWindow.xaml.cs:77](src/GUI/MainWindow.xaml.cs#L77) hace `new DashboardPage(_services)`. El constructor:

1. Se suscribe a `OccupancyChanged` de **cada `ParkingSpot`** ([DashboardPage.xaml.cs:26-27](src/GUI/Pages/DashboardPage.xaml.cs#L26-L27)).
2. Se suscribe al `Loaded` ([DashboardPage.xaml.cs:24](src/GUI/Pages/DashboardPage.xaml.cs#L24)).

**Nunca desuscribe**, así que cada visita a la pestaña deja una `DashboardPage` "fantasma" suscrita al dominio. La página queda viva en memoria y sigue refrescándose en segundo plano cada vez que cambia un sensor.

**Mejora.**

- Cambiar a `Frame.Navigate(typeof(DashboardPage))` y manejar `OnNavigatedTo` / `OnNavigatedFrom` para registrar y liberar suscripciones.
- Si el `Frame` se configura con `NavigationCacheMode.Required`, la página se reutiliza en lugar de instanciarse de nuevo.
- Centralizar la lógica de "suscribirse en `OnNavigatedTo`, liberar en `OnNavigatedFrom`" con un `CompositeDisposable`.

---

## 9. `async void` y Manejo de Errores *(nuevo)*

**Problema actual.**

- [DashboardPage.xaml.cs:30](src/GUI/Pages/DashboardPage.xaml.cs#L30): `private async void OnRefreshClick(...)`.
- [AdminPage.xaml.cs:23](src/GUI/Pages/AdminPage.xaml.cs#L23): `Loaded += async (_, _) => await ReloadAsync();`.
- [DashboardPage.xaml.cs:131-132](src/GUI/Pages/DashboardPage.xaml.cs#L131) (en `ParkingServices.cs`): `_ = repository.UpdateSpotStatusAsync(evt.SpotId, evt.IsOccupied);` — fire-and-forget sin observación de excepciones.

`async void` impide que las excepciones se propaguen y `_ = TaskFire` las descarta silenciosamente.

**Mejora.**

- Migrar los handlers a `ICommand` (`AsyncRelayCommand` del CommunityToolkit) que **sí** captura excepciones de forma estructurada y soporta `CanExecute` para deshabilitar el botón durante la operación.
- Para los `Loaded += async …`, mover la carga inicial al ciclo de vida del ViewModel (`InitializeAsync()`) con `CancellationToken` propagado desde el `OnNavigatedTo`.
- Las tareas fire-and-forget deben pasar por un helper que registre la excepción en el `ILogger` o en una política de telemetría.

---

## 10. Acceso a Recursos por Clave de Cadena *(nuevo)*

**Problema actual.** El code-behind resuelve pinceles por **clave de string** contra el diccionario global de la `App`:

- [DashboardPage.xaml.cs:78](src/GUI/Pages/DashboardPage.xaml.cs#L78): `(Brush)XamlApp.Current.Resources["Tx2Brush"]`
- [DashboardPage.xaml.cs:229](src/GUI/Pages/DashboardPage.xaml.cs#L229): `(Brush)res["DangerBrush"]`
- [MainWindow.xaml.cs:103](src/GUI/MainWindow.xaml.cs#L103): `(Brush)XamlApp.Current.Resources["SuccessBrush" : "DangerBrush"]`

Esto rompe en silencio si se renombra una clave, no respeta `ThemeDictionaries` (los pinceles se quedan congelados al tema activo en el momento de la asignación), y obliga a tener todos los recursos en el `App` global.

**Mejora.**

- Mover el cableado de pinceles al XAML mediante `{ThemeResource DangerBrush}`, que sí se reevalúa al cambiar el tema del sistema.
- Donde haga falta resolver por código, encapsular en una clase tipada (`AppBrushes.Danger`, `AppBrushes.Success`) que centralice los `try/cast` y permita detectar claves ausentes con un test simple.

---

## 11. Tematización, Localización y Accesibilidad *(nuevo)*

**Problema actual.**

- Los textos en español están hard-codeados en cada vista (`"Sin alertas"`, `"Mostrando {n} de {n} spots"`, `"Arduino — desconectado"`).
- No hay `AutomationProperties.Name` en los iconos/`FontIcon` ([DashboardPage.xaml.cs:142-152](src/GUI/Pages/DashboardPage.xaml.cs#L142-L152)), por lo que lectores de pantalla no los anuncian.
- El soporte de modo oscuro/claro depende de la `App` y de los pinceles resueltos manualmente (§10).

**Mejora.**

- Adoptar `x:Uid` + archivos `.resw` por idioma. Por ahora basta con `Resources/es-ES/Resources.resw`; la estructura está lista cuando se necesite añadir inglés.
- Añadir `AutomationProperties.Name` y `AutomationProperties.HelpText` a iconografía no textual.
- Usar `ThemeDictionaries` para que los pinceles "Tx1/Tx2/Tx3/Success/Danger/…" tengan variantes Light/Dark; el binding por `ThemeResource` los aplicará automáticamente.

---

## 12. Bootstrap, Diseño y Testabilidad *(nuevo)*

**Problema actual.** [ParkingServices.BootstrapAsync](src/GUI/Bootstrap/ParkingServices.cs#L70) ensambla repositorio, bus, sensores, dispatcher, handlers y bridge en un único método de 100+ líneas. Es lo opuesto a una composición declarativa y es imposible de instrumentar para tests de integración de la UI.

**Mejora.**

- Partir el bootstrap en `IServiceCollection` *extensions*: `AddDomain()`, `AddPersistence(connectionString)`, `AddHardware(config)`, `AddViewModels()`. Cada uno encapsula su propio cableado.
- Proveer un **modo "mock"** activable por config (`hardware.json` con `"port": "MOCK"`) que sustituya el `ArduinoSerialBridge` por una implementación en memoria. Hace que la GUI sea demostrable sin hardware y testeable en CI.
- Añadir **datos de diseño** (`d:DataContext`) para que los ViewModels rendericen ejemplo en el diseñador XAML.

---

## Capacidades Adicionales del Framework (Oportunidades Futuras)

Existen opciones nativas de **WinUI 3** que actualmente no se explotan y que mejorarían el UX y la limpieza del código:

1. **Commands (`ICommand`):** Migrar los `OnRefreshClick(object sender, …)` hacia implementaciones de `ICommand` enlazadas al botón (`Command="{x:Bind ViewModel.RefreshCommand}"`). Ver §9.
2. **`ItemsRepeater` con virtualización:** Para `RowsPanel` y listas largas en `AdminPage`, evita instanciar todas las filas (ver §7).
3. **Connected Animations y `Animations.Implicit`:** Transiciones suaves al cambiar de pestaña.
4. **`ThemeDictionaries` + `ThemeResource`:** Soporte limpio de modo oscuro/claro sin tocar code-behind (ver §10–11).
5. **`x:Bind` con funciones:** Permite expresiones tipo `{x:Bind ViewModel.OccupancyText(Occupied, Total), Mode=OneWay}` y evita métodos `Refresh()` manuales.
6. **`Frame.Navigate` + `NavigationCacheMode`:** Sustituye al swap manual de `ContentFrame.Content = new XPage(...)` (§8).
7. **`Microsoft.Extensions.DependencyInjection` + `IHostBuilder`:** Ya soportado en aplicaciones WinUI 3 — habilita el contenedor estándar (§3).
8. **CommunityToolkit.Mvvm:** Source generators para `[ObservableProperty]`, `[RelayCommand]`, `IMessenger`; reduce el boilerplate de §1, §5 y §9.

---

## Resumen — Orden Recomendado de Refactorización

Para minimizar el riesgo, una secuencia incremental sería:

1. **Bootstrap → DI** (§3, §12): permite que cada paso siguiente se haga página por página sin tocar el resto.
2. **Capa Application: queries + DTOs** (§2): desacopla la vista del repositorio antes de moverla a MVVM.
3. **MVVM en una página piloto** (§1) — recomendable empezar por `DashboardPage`.
4. **Eventos observables + ciclo de vida de navegación** (§5, §6, §8) sobre la misma página piloto.
5. **Migrar code-behind visual a XAML/DataTemplates** (§7) en la página piloto.
6. **Hardware behind interface** (§4) y **commands/error handling** (§9).
7. **Tematización, recursos tipados, localización, accesibilidad** (§10, §11) como capa transversal final.

Cada paso es independiente y mergeable por separado; el dominio (`Core`/`Application`) no necesita cambios para ninguno de ellos salvo el §2.

---

# Spec de Implementación Agéntica

Esta sección define **cómo ejecutar el plan anterior con un orquestador + subagentes** en Claude Code (herramienta `Agent`). Está pensada para minimizar el tiempo de pared aprovechando que varias mejoras son independientes entre sí.

## A. Filosofía del flujo

- **El orquestador no escribe código.** Solo planifica, despacha agentes, integra resultados y verifica.
- **Cada agente recibe un prompt autocontenido.** No comparte memoria con el orquestador: hay que darle archivos, líneas y criterios de aceptación explícitos.
- **Paralelismo siempre que sea seguro.** Dos agentes pueden trabajar en paralelo **solo si tocan archivos disjuntos** o si trabajan en *worktrees* aislados (skill `superpowers:using-git-worktrees`).
- **Sincronización por fase.** Al final de cada fase, el orquestador hace `dotnet build`, corre los tests y mergea (o pide al humano que mergee) antes de disparar la siguiente fase.

## B. Grafo de dependencias y oleadas de paralelismo

```
Fase 0 — Prep (secuencial)
    └── F0.1  Crear ramas/worktrees, snapshot de baseline (build + tests verdes)

Fase 1 — Cimientos (todo paralelo, mismo worktree base)
    ├── F1.A  DI container + bootstrap modular            (§3, §12)
    ├── F1.B  IHardwareStatus + adapter sobre Bridge      (§4)
    └── F1.C  ThemeDictionaries + AppBrushes tipados      (§10)
            ↓ merge a integración
            ↓ build + smoke test

Fase 2 — Capa de aplicación y observabilidad (paralelo)
    ├── F2.A  Queries/DTOs (LotSnapshot, SpotRow, …)     (§2)
    └── F2.B  Bus/Observable de snapshots + IUiDispatcher (§5, §6)
            ↓ merge

Fase 3 — Piloto MVVM Dashboard (secuencial, depende de F1+F2)
    └── F3.1  DashboardViewModel + XAML bindings          (§1)
    └── F3.2  ItemsRepeater + DataTemplates en Dashboard  (§7, parcial)
    └── F3.3  Ciclo de vida navegación + Commands         (§8, §9)

Fase 4 — Replicación por página (paralelo, una página por agente)
    ├── F4.A  MapPage         (§1, §5–9 aplicado)
    ├── F4.B  LogPage         (idem)
    ├── F4.C  AdminPage       (idem + virtualización)
    └── F4.D  HardwarePage    (idem)
            ↓ merge

Fase 5 — Transversales (paralelo)
    ├── F5.A  Localización (.resw, x:Uid)                 (§11)
    ├── F5.B  Accesibilidad (AutomationProperties)        (§11)
    └── F5.C  Modo mock de hardware                       (§12)
```

**Cuellos de botella reales:** F0, el merge entre fases y F3 (que es secuencial por diseño porque sirve de plantilla para F4).

## C. Roles

| Rol | Responsabilidad | Herramienta |
|---|---|---|
| **Orquestador** | Lee este documento, mantiene el grafo, despacha agentes, mergea, verifica build/tests, decide cuándo avanzar de fase. | Sesión principal de Claude Code |
| **Agente Implementador** | Ejecuta una tarea concreta (un nodo del grafo). Escribe código, corre tests, reporta. | `Agent` con `subagent_type: "general-purpose"` |
| **Agente Revisor** | Lee el diff producido por un implementador y emite veredicto. Despachado tras cada `Fx.*` antes del merge. | `Agent` con `subagent_type: "general-purpose"` (o el revisor del proyecto si existe) |
| **Agente Explorador** | Búsquedas puntuales (¿dónde más se usa este símbolo?). | `Agent` con `subagent_type: "Explore"` |

## D. Aislamiento

- **Fase 1** y **Fase 4**: cada agente trabaja en un **worktree git aislado** (`Agent` con `isolation: "worktree"`), porque el riesgo de colisión de archivos es real.
- **Fase 2, 3, 5**: la coordinación de archivos es suficientemente clara como para usar el árbol principal, siempre que solo haya **un implementador activo por archivo**.

## E. Prompt del Orquestador (copy-paste)

> Pega este prompt al iniciar la sesión orquestadora. Asume que el documento `docs/md/mejoras-gui-arquitectura.md` está en el repo.

```text
Eres el orquestador de una refactorización de la capa GUI (WinUI 3) del proyecto
Smart Parking Lot. NO escribes código tú: tu trabajo es planificar, despachar
agentes y verificar.

CONTEXTO OBLIGATORIO ANTES DE EMPEZAR:
1. Lee docs/md/mejoras-gui-arquitectura.md en su totalidad. Es la fuente de verdad.
2. Lee .claude/CLAUDE.md para convenciones del proyecto.
3. Verifica baseline: `dotnet build` debe pasar antes de tocar nada. Si no pasa,
   detente y reporta.

PROTOCOLO POR FASE (repetir para cada fase del grafo en §B del documento):
  a. Identifica los nodos paralelizables de la fase actual.
  b. Para cada nodo, despacha un Agente Implementador con su plantilla de prompt
     correspondiente (§F del documento). Los nodos paralelos se despachan en una
     ÚNICA respuesta con múltiples bloques `Agent` simultáneos.
  c. Espera a que todos los agentes de la fase terminen.
  d. Para cada agente que reporta cambios, despacha un Agente Revisor sobre su
     diff. Los revisores también pueden correr en paralelo.
  e. Si todos los revisores aprueban: mergea los worktrees al árbol principal y
     corre `dotnet build` + `dotnet test`. Si falla, abre un nodo de "fix" y
     vuelve a (b). Si pasa, avanza a la siguiente fase.
  f. Después de cada fase, escribe un resumen breve (≤5 líneas) al usuario:
     qué se cerró, qué viene, riesgos abiertos.

REGLAS DE SEGURIDAD:
- Nunca hagas push ni abras PR sin que el usuario lo pida explícitamente.
- Nunca corras git reset --hard ni borres branches sin confirmación.
- Si un agente reporta build roto que no puede arreglar, NO intentes parchearlo
  desde la sesión orquestadora: despacha un nuevo agente de fix con el error
  literal pegado en su prompt.
- Una sola pregunta al humano por fase, agrupada al final (no preguntes por
  cada nodo).

CRITERIO DE ÉXITO GLOBAL:
- Todas las secciones §1–§12 del documento están implementadas.
- `dotnet build` y `dotnet test` pasan en main.
- Ninguna página de GUI accede directamente a IParkingRepository ni a
  ArduinoSerialBridge.
- ParkingServices ya no existe o es un mero composite root sin propiedades
  públicas consumidas por las páginas.

EMPIEZA por la Fase 0.
```

## F. Plantillas de Prompt por Nodo

Cada plantilla está pensada para pegarse íntegra como `prompt` del `Agent`. Los nodos paralelos se despachan en **una sola respuesta del orquestador con múltiples tool-calls `Agent` simultáneos**.

### F1.A — DI Container + Bootstrap Modular

```text
Tarea: introducir Microsoft.Extensions.DependencyInjection en el proyecto GUI y
romper src/GUI/Bootstrap/ParkingServices.cs en extensiones de IServiceCollection.

Contexto del proyecto: lee docs/md/mejoras-gui-arquitectura.md §3 y §12.

Alcance EXACTO:
- Añadir paquete Microsoft.Extensions.DependencyInjection al csproj de GUI.
- Crear src/GUI/Bootstrap/ServiceCollectionExtensions.cs con métodos:
    AddDomain(), AddPersistence(string connStr), AddHardware(HardwareConfig),
    AddApplicationServices(), AddGuiViewModels()
- App.xaml.cs construye un IServiceProvider en OnLaunched y lo expone como
  App.Services (estático). El MainWindow se resuelve vía DI.
- ParkingServices DEJA DE EXISTIR como god object. Su contenido se migra a las
  extensiones. Si algo no se puede migrar todavía (p. ej., consumido por una
  página que aún no está refactorizada), expónlo temporalmente como singleton
  individual en el contenedor, NUNCA como una clase agregadora.

NO TOQUES:
- Nada dentro de src/Application, src/Core, src/Persistence, src/Hardware.
- Los .xaml.cs de páginas individuales (los refactorizan otros agentes).
- Solo edita MainWindow.xaml.cs para reemplazar `new XPage(_services)` por
  `App.Services.GetRequiredService<XPage>()` y registra cada Page como
  AddTransient<XPage>().

CRITERIO DE ACEPTACIÓN:
- `dotnet build src/GUI` pasa sin warnings nuevos.
- `grep -r "ParkingServices" src/GUI` no devuelve referencias activas (solo
  comentarios o el propio archivo si decides dejarlo como deprecated).
- La GUI arranca y muestra Dashboard sin errores en runtime (intenta `dotnet run`
  unos segundos y mata el proceso; reporta logs si falla).

Reporta en ≤200 palabras: archivos creados, archivos eliminados, cambios en
csproj, y cualquier desviación del alcance.
```

### F1.B — IHardwareStatus

```text
Tarea: introducir la abstracción IHardwareStatus en la capa Application y un
adapter sobre ArduinoSerialBridge en la capa Hardware. La GUI debe consumir
IHardwareStatus, no Bridge.

Contexto: docs/md/mejoras-gui-arquitectura.md §4.

Alcance EXACTO:
- Nuevo archivo src/Application/Hardware/IHardwareStatus.cs:
    public interface IHardwareStatus {
        bool IsConnected { get; }
        string DisplayName { get; }
        event EventHandler? Changed;
    }
- Nueva implementación src/Hardware/ArduinoHardwareStatus.cs que envuelve
  ArduinoSerialBridge y dispara Changed cuando IsListening cambia.
- En MainWindow.xaml.cs (líneas 96-105 actuales), reemplazar el acceso a
  _services.Bridge.IsListening y _services.Config.Port por una propiedad
  inyectada IHardwareStatus _status.
- NO inventes una capacidad de "Changed" si el Bridge actual no la expone; en
  ese caso, dispara Changed manualmente desde dentro de StartListening/StopListening
  vía un evento agregado al Bridge. Documenta esta decisión.

CRITERIO DE ACEPTACIÓN:
- `dotnet build` pasa.
- `grep -r "ArduinoSerialBridge" src/GUI` solo devuelve App.xaml.cs / bootstrap.
- La cadena literal "Arduino" no aparece en código de páginas, solo en la
  configuración o el DisplayName del adapter.

Reporta como F1.A.
```

### F1.C — ThemeDictionaries + AppBrushes tipados

```text
Tarea: migrar el acceso a pinceles por clave de string a ThemeDictionaries y
un wrapper tipado AppBrushes.

Contexto: docs/md/mejoras-gui-arquitectura.md §10.

Alcance EXACTO:
- Reorganizar src/GUI/Styles/Theme.xaml para definir <ResourceDictionary.ThemeDictionaries>
  con variantes "Light", "Dark", "HighContrast" de TODOS los pinceles
  ("Tx1Brush", "Tx2Brush", "Tx3Brush", "Layer1Brush", "Layer2Brush",
  "Stroke2Brush", "SuccessBrush", "WarningBrush", "DangerBrush", "AccentBrush").
  Pinta las variantes Dark con la paleta actual y deriva Light por contraste
  razonable (no obsesionarse con perfección visual, solo que cargue).
- Crear src/GUI/Resources/AppBrushes.cs:
    public static class AppBrushes {
        public static Brush Tx1 => Resolve("Tx1Brush");
        ...
        private static Brush Resolve(string key) =>
            (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    }
- Sustituir TODAS las apariciones de XamlApp.Current.Resources["XBrush"] en
  src/GUI/**/*.cs por AppBrushes.X. Lista de archivos a tocar:
    - src/GUI/MainWindow.xaml.cs
    - src/GUI/Pages/DashboardPage.xaml.cs
    - src/GUI/Pages/AdminPage.xaml.cs
    - (y cualquier otro que aparezca al hacer grep)

NO RENOMBRES claves XAML — solo introduce el wrapper.

CRITERIO DE ACEPTACIÓN:
- `dotnet build` pasa.
- `grep -rn "Application.Current.Resources\[" src/GUI` no devuelve nada fuera
  de AppBrushes.cs.
- La GUI arranca; si el tema del sistema es Dark, los pinceles siguen luciendo
  como antes.

Reporta como F1.A.
```

### F2.A — Queries + DTOs en Application

```text
Tarea: crear casos de uso de lectura en la capa Application para que la GUI
deje de consumir IParkingRepository.

Contexto: docs/md/mejoras-gui-arquitectura.md §2.

Alcance EXACTO:
- Nuevos archivos en src/Application/Queries/:
    - LotSnapshotDto.cs: record con Id, Name, TotalSpots, OccupiedSpots,
      ZoneSummaries (lista de ZoneSummaryDto { Zone, Occupied, Total }),
      Gates (lista de GateSummaryDto { GateId, Type, IsOpen }).
    - SpotRowDto.cs: record con Id, Zone, Type, Address, IsOccupied, Floor.
    - IGetLotSnapshotQuery.cs + GetLotSnapshotQuery.cs.
    - IGetSpotRowsQuery.cs + GetSpotRowsQuery.cs.
- Estas queries reciben IParkingRepository por constructor y devuelven DTOs.
- NO toques aún las páginas — solo crea las queries y regístralas si F1.A ya
  fue mergeada (en cuyo caso ya hay extensiones de DI; añádelas a
  AddApplicationServices). Si F1.A NO está mergeado todavía, deja las queries
  sin registrar y avisa al orquestador.

CRITERIO DE ACEPTACIÓN:
- `dotnet build` pasa.
- Existen tests unitarios mínimos para cada query bajo tests/Application.Tests/
  que mockeen el repositorio y validen la proyección.

Reporta como F1.A.
```

### F2.B — Observable Snapshots + IUiDispatcher

```text
Tarea: introducir un flujo observable de snapshots del estado del Lot y una
abstracción IUiThreadDispatcher.

Contexto: docs/md/mejoras-gui-arquitectura.md §5 y §6.

Alcance EXACTO:
- Nuevo src/Application/Observability/ILotSnapshotStream.cs que expone
  `event EventHandler<LotSnapshotDto>? SnapshotChanged;` y un método
  `LotSnapshotDto Current()`. NO uses System.Reactive todavía (mantén la
  superficie pequeña).
- Implementación src/Application/Observability/LotSnapshotStream.cs que:
    - Recibe ParkingLot y IGetLotSnapshotQuery (de F2.A) por constructor.
    - Se suscribe internamente a OccupancyChanged de cada spot del lot.
    - Por cada cambio, recalcula snapshot y emite SnapshotChanged.
- Nuevo src/GUI/Infrastructure/IUiThreadDispatcher.cs +
  DispatcherQueueUiThreadDispatcher.cs (wrapper sobre DispatcherQueue).
- Registrar ambos en las extensiones de DI (singleton).

NO ELIMINES todavía las suscripciones directas a OccupancyChanged en las páginas:
otros agentes lo harán en F3+. Solo introduce la infraestructura.

CRITERIO DE ACEPTACIÓN:
- `dotnet build` pasa.
- Test que verifica que tras togglear el spot, SnapshotChanged se dispara.

Reporta como F1.A.
```

### F3 — Piloto MVVM Dashboard (un solo agente, secuencial)

```text
Tarea: refactorizar DashboardPage al patrón MVVM completo. Esta página servirá
de plantilla para el resto: hazla limpia y deja un breve docs/aux-uml/
mvvm-template.md (≤30 líneas) explicando el patrón al final.

Contexto: docs/md/mejoras-gui-arquitectura.md §1, §5, §6, §7, §8, §9.

Pre-requisitos verificables ANTES de empezar (si no se cumplen, detente):
- src/GUI/Bootstrap/ServiceCollectionExtensions.cs existe (F1.A).
- src/Application/Hardware/IHardwareStatus.cs existe (F1.B).
- src/Application/Queries/LotSnapshotDto.cs existe (F2.A).
- src/Application/Observability/ILotSnapshotStream.cs existe (F2.B).
- src/GUI/Resources/AppBrushes.cs existe (F1.C).

Alcance EXACTO:
- Añadir CommunityToolkit.Mvvm al csproj.
- Crear src/GUI/ViewModels/DashboardViewModel.cs con [ObservableProperty] en
  Occupied, Available, OccupancyPercent, Zones (ObservableCollection),
  Alerts (ObservableCollection), Gates (ObservableCollection), HardwareName,
  IsHardwareConnected, LastUpdated.
- Inyecta ILotSnapshotStream, IHardwareStatus, IUiThreadDispatcher.
- Comando RefreshCommand vía [RelayCommand].
- Refactorizar DashboardPage.xaml para:
    - Eliminar TileOccupied/TilePct/etc. nominados; usarlos como {x:Bind
      ViewModel.Occupied, Mode=OneWay}.
    - Reemplazar ZonesPanel, AlertsPanel, GatesPanel por ItemsRepeater (o
      ItemsControl) con DataTemplates declarativos.
- DashboardPage.xaml.cs queda con: constructor que recibe DashboardViewModel,
  asignación a DataContext, override de OnNavigatedTo/OnNavigatedFrom para
  Initialize/Dispose del ViewModel. Sin suscripciones a OccupancyChanged.
- En App.xaml.cs registrar AddTransient<DashboardViewModel>().

CRITERIO DE ACEPTACIÓN:
- `dotnet build` pasa.
- `grep -n "OccupancyChanged" src/GUI/Pages/DashboardPage.xaml.cs` → 0 hits.
- `grep -n "TryEnqueue" src/GUI/Pages/DashboardPage.xaml.cs` → 0 hits.
- `grep -n "Repository" src/GUI/Pages/DashboardPage.xaml.cs` → 0 hits.
- `grep -n "new Grid\|new Border\|new StackPanel" src/GUI/Pages/DashboardPage.xaml.cs`
  → 0 hits.
- La GUI arranca, navega al Dashboard, los tiles muestran ocupación real, al
  togglear un spot la UI se actualiza sin pulsar Refresh.

Si encuentras desviaciones del plan original, anótalas en el reporte pero NO
las arregles silenciosamente.

Reporta en ≤300 palabras.
```

### F4.A–D — Replicación por Página (4 agentes paralelos)

```text
Tarea: aplicar el patrón MVVM ya establecido en DashboardPage a {PAGE_NAME}.

Plantilla obligatoria: lee primero docs/aux-uml/mvvm-template.md y
src/GUI/Pages/DashboardPage.xaml + DashboardPage.xaml.cs +
src/GUI/ViewModels/DashboardViewModel.cs. Imita su estructura.

Alcance EXACTO para {PAGE_NAME}:
- Crear src/GUI/ViewModels/{PAGE_NAME}ViewModel.cs.
- Reescribir src/GUI/Pages/{PAGE_NAME}.xaml + .xaml.cs según los mismos
  criterios que F3 (0 referencias a Repository, OccupancyChanged, TryEnqueue,
  construcción manual de paneles).
- Registrar el ViewModel en AddGuiViewModels().

NO TOQUES archivos fuera de:
  - src/GUI/Pages/{PAGE_NAME}.xaml{,.cs}
  - src/GUI/ViewModels/{PAGE_NAME}ViewModel.cs
  - src/GUI/Bootstrap/ServiceCollectionExtensions.cs (solo añadir tu
    AddTransient — usa Edit para no pisar a otros agentes).

CASOS PARTICULARES:
- AdminPage: usa ItemsRepeater con virtualización (ScrollViewer + ItemsRepeater),
  porque la lista puede crecer. El filtro debe ir en el ViewModel, no en code-behind.
- LogPage: el ViewModel se suscribe a GuiLogger (que ya es observable por
  Snapshot()); evalúa si conviene introducir un ILogStream similar a
  ILotSnapshotStream.
- HardwarePage: consume IHardwareStatus (F1.B) en lugar de Bridge directo.
- MapPage: si dibuja un canvas, mantenlo en code-behind pero recibe el estado
  desde el ViewModel.

CRITERIO DE ACEPTACIÓN: idénticos greps a F3, aplicados a {PAGE_NAME}.

Reporta en ≤250 palabras.
```

> El orquestador despacha F4.A/B/C/D en **una sola respuesta con cuatro `Agent`
> simultáneos**, cada uno con `isolation: "worktree"`.

### F5.A — Localización (paralelo)

```text
Tarea: extraer strings hard-codeados en español a archivos .resw y referenciarlos
con x:Uid.

Contexto: docs/md/mejoras-gui-arquitectura.md §11.

Alcance EXACTO:
- Crear src/GUI/Strings/es-ES/Resources.resw con TODOS los literales visibles
  detectados en las páginas (incluye "Sin alertas", "Mostrando {0} de {1} spots",
  "Arduino — desconectado", encabezados, tooltips, botones, etc.).
- Reemplazar literales en XAML por x:Uid + propiedad correspondiente.
- Para literales en code-behind/ViewModels, usar
  ResourceLoader.GetForViewIndependentUse().GetString("Key").

NO añadas inglés todavía — solo prepara la infraestructura.

CRITERIO DE ACEPTACIÓN:
- La GUI arranca y todo se ve igual.
- `grep -nE "\"[A-ZÁÉÍÓÚ][a-záéíóú ]{5,}\"" src/GUI/**/*.xaml` retorna ≤5 hits
  (los inevitables: claves técnicas, formatos numéricos, etc.).
```

### F5.B — Accesibilidad (paralelo)

```text
Tarea: añadir AutomationProperties a iconografía y controles no textuales.

Contexto: docs/md/mejoras-gui-arquitectura.md §11.

Alcance EXACTO:
- Para cada FontIcon, Image, o control sin texto en src/GUI/**/*.xaml:
    AutomationProperties.Name="..."
    AutomationProperties.HelpText="..." (cuando sea útil)
- Para badges de estado dinámico, vincular AutomationProperties.Name al
  ViewModel para que se anuncie el estado real ("Puerta entrada: abierta").
- Verifica navegación con Tab: tab-stops razonables, foco visible.

NO toques layout ni estilos.

CRITERIO DE ACEPTACIÓN:
- Inspección manual con Accessibility Insights for Windows (anótalo, no
  requiere herramienta en CI).
- Cada FontIcon del repo tiene AutomationProperties.Name.
```

### F5.C — Modo Mock de Hardware (paralelo)

```text
Tarea: permitir arrancar la GUI sin Arduino físico, leyendo hardware.json con
"port": "MOCK".

Contexto: docs/md/mejoras-gui-arquitectura.md §12.

Alcance EXACTO:
- Crear src/Hardware/MockArduinoBridge.cs implementando la misma superficie
  pública que ArduinoSerialBridge (StartListening, StopListening, IsListening,
  evento de lecturas).
- En AddHardware(HardwareConfig), decidir entre real o mock según config.Port.
- El mock genera lecturas sintéticas de ocupación cada N segundos
  (configurable, default 5s) ciclando entre spots aleatorios. Usa Random con
  semilla fija para reproducibilidad en demos.

CRITERIO DE ACEPTACIÓN:
- Con hardware.json port="MOCK", la GUI arranca, no abre puerto serie, y los
  spots cambian solos cada pocos segundos.
- Con port="COM3" (o el real), el comportamiento es idéntico al actual.
```

## G. Plantilla del Agente Revisor

```text
Tarea: revisar el diff producido por el agente {NODE_ID}.

Lee:
- docs/md/mejoras-gui-arquitectura.md (sección §{REFS}).
- El criterio de aceptación literal del prompt original (te lo paso abajo).
- El diff: `git diff <base>..HEAD -- <ruta>`.

Veredicto en formato:
  STATUS: APROBADO | CAMBIOS_REQUERIDOS | RECHAZADO
  RAZONES: (3-6 bullets)
  ACCIONES: (qué tendría que hacer el implementador para llegar a APROBADO,
    si aplica)

Verifica especialmente:
- ¿Se cumplió cada grep listado en el criterio de aceptación?
- ¿Hay archivos tocados fuera del alcance declarado?
- ¿Aparecen comentarios de "patrón GRASP / MVVM / etc." en el código? (debe
  haber CERO; ver feedback_no_design_comments en la memoria del proyecto).
- ¿Hay TODOs o `throw new NotImplementedException` sin justificación?
- ¿`dotnet build` y `dotnet test` pasan?

NO escribas código. Solo veredicto + razones.
Criterio de aceptación literal a continuación:
---
{PEGA_AQUI_EL_CRITERIO_DE_ACEPTACION_DEL_NODO}
---
```

## H. Protocolo de Sincronización entre Fases

Después de cada fase, el orquestador ejecuta este check-list **en su propia sesión** (no delegado):

1. `git status` en el árbol principal (deben estar todos los merges aplicados).
2. `dotnet build` — falla ⇒ abrir nodo de fix con el error literal.
3. `dotnet test` — idem.
4. `dotnet run --project src/GUI` por 10s, capturar logs, matar el proceso —
   smoke test runtime.
5. Comparar el grafo §B contra lo cerrado: ¿algún criterio de aceptación
   quedó incumplido aunque el agente reportó OK? Si sí, despachar un
   sub-nodo de remediación.
6. Resumen al usuario (≤5 líneas) + pregunta única si la hay (p. ej., "¿paso
   a F4 o quieres ajustar algo del piloto?").

## I. Cómo Empezar Mañana

El usuario solo necesita pegar el **Prompt del Orquestador (§E)** en una sesión
nueva de Claude Code, con este documento ya en el repo. El orquestador
descubre el resto.
