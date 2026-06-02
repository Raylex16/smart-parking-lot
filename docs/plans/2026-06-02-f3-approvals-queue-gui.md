# F3: Cola de Aprobaciones en Vivo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir a la página "Control de Acceso" una sección en vivo con las aprobaciones pendientes del modo MANUAL: cada solicitud muestra placa, gate, tiempo transcurrido y botones Aprobar/Denegar que la resuelven en tiempo real.

**Architecture:** `PendingApprovalRowVm` encapsula un `PendingApproval` con un timer de 1s. `AccessControlViewModel` se suscribe al evento `IApprovalQueue.Enqueued` en `Activate()` (patrón idéntico al log en vivo de `HardwarePageViewModel`). El XAML de `AdminPage` reemplaza el placeholder de F2 por la sección real.

**Tech Stack:** WinUI 3, CommunityToolkit.Mvvm, `DispatcherQueue` para actualizaciones de UI desde el timer.

**Prerequisito:** F2 debe estar completa (página Control de Acceso funcional con el placeholder de aprobaciones).

---

## Mapa de archivos

| Acción | Archivo |
|--------|---------|
| Create | `src/GUI/ViewModels/PendingApprovalRowVm.cs` |
| Modify | `src/GUI/ViewModels/AccessControlViewModel.cs` |
| Modify | `src/GUI/Pages/AdminPage.xaml` |
| Modify | `src/GUI/Bootstrap/ServiceCollectionExtensions.cs` |

---

### Task 1: Crear `PendingApprovalRowVm`

**Files:**
- Create: `src/GUI/ViewModels/PendingApprovalRowVm.cs`

- [ ] **Step 1: Crear el VM de fila con timer**

```csharp
// src/GUI/ViewModels/PendingApprovalRowVm.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Core.Approvals;

namespace SmartParkingLot.Gui.ViewModels;

public partial class PendingApprovalRowVm : ObservableObject, IDisposable
{
    private readonly DispatcherQueueTimer _timer;

    public string Id      { get; }
    public string Plate   { get; }
    public string GateId  { get; }

    [ObservableProperty] private int    _elapsedSeconds;
    [ObservableProperty] private bool   _isResolved;

    public string ElapsedLabel => $"hace {ElapsedSeconds}s";

    public IRelayCommand ApproveCommand { get; }
    public IRelayCommand DenyCommand    { get; }

    public PendingApprovalRowVm(
        PendingApproval approval,
        IApprovalDecisionService decisions,
        DispatcherQueue ui)
    {
        Id     = approval.Id;
        Plate  = approval.VehiclePlate;
        GateId = approval.GateId;

        ApproveCommand = new RelayCommand(() =>
        {
            if (IsResolved) return;
            decisions.Resolve(Id, approved: true);
            IsResolved = true;
            _timer.Stop();
        });

        DenyCommand = new RelayCommand(() =>
        {
            if (IsResolved) return;
            decisions.Resolve(Id, approved: false);
            IsResolved = true;
            _timer.Stop();
        });

        _timer = ui.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            ElapsedSeconds++;
            OnPropertyChanged(nameof(ElapsedLabel));
            if (approval.IsResolved)
            {
                IsResolved = true;
                _timer.Stop();
            }
        };
        _timer.Start();
    }

    public void Dispose() => _timer.Stop();
}
```

- [ ] **Step 2: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/GUI/ViewModels/PendingApprovalRowVm.cs
git commit -m "feat(gui): PendingApprovalRowVm con timer de 1s y comandos Aprobar/Denegar"
```

---

### Task 2: Extender `AccessControlViewModel` con la cola de aprobaciones

**Files:**
- Modify: `src/GUI/ViewModels/AccessControlViewModel.cs`

- [ ] **Step 1: Añadir dependencias y miembros de la cola**

Añadir en la clase `AccessControlViewModel` las dependencias nuevas. Localizar el constructor actual y reemplazarlo por la versión ampliada, y añadir los nuevos miembros:

```csharp
// Añadir estos using al inicio del archivo:
using Microsoft.UI.Dispatching;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Core.Approvals;
```

Añadir los nuevos campos privados a la clase (después de los existentes):
```csharp
private readonly IApprovalQueue            _approvalQueue;
private readonly IApprovalDecisionService  _approvalDecisions;
private readonly DispatcherQueue           _dq;
private Action<PendingApproval>?           _approvalHandler;
```

Añadir la propiedad pública:
```csharp
public ObservableCollection<PendingApprovalRowVm> PendingApprovals { get; } = new();
```

Añadir la propiedad computed:
```csharp
[ObservableProperty] private string _approvalsBadgeText = "Sin pendientes";
```

- [ ] **Step 2: Reemplazar el constructor con la versión que acepta las nuevas dependencias**

```csharp
public AccessControlViewModel(
    IParkingModeService modeService,
    IAccessPolicyConfigService configService,
    ILotSnapshotStream stream,
    IUiThreadDispatcher ui,
    IApprovalQueue approvalQueue,
    IApprovalDecisionService approvalDecisions,
    DispatcherQueue dq)
{
    _modeService       = modeService;
    _configService     = configService;
    _stream            = stream;
    _ui                = ui;
    _approvalQueue     = approvalQueue;
    _approvalDecisions = approvalDecisions;
    _dq                = dq;
}
```

- [ ] **Step 3: Extender `Activate()` y `Deactivate()`**

Reemplazar los métodos `Activate()` y `Deactivate()` existentes:

```csharp
public void Activate()
{
    _ = LoadAsync();

    // Cargar aprobaciones ya encoladas
    foreach (var a in _approvalQueue.GetPending())
        AddApprovalRow(a);

    // Suscribir a nuevas aprobaciones
    _approvalHandler = approval => _ui.Enqueue(() => AddApprovalRow(approval));
    _approvalQueue.Enqueued += _approvalHandler;
}

public void Deactivate()
{
    if (_approvalHandler is not null)
        _approvalQueue.Enqueued -= _approvalHandler;

    foreach (var row in PendingApprovals)
        row.Dispose();
    PendingApprovals.Clear();
}
```

- [ ] **Step 4: Añadir el método `AddApprovalRow`**

```csharp
private void AddApprovalRow(PendingApproval approval)
{
    var row = new PendingApprovalRowVm(approval, _approvalDecisions, _dq);
    row.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(PendingApprovalRowVm.IsResolved) && row.IsResolved)
        {
            _ui.Enqueue(() =>
            {
                PendingApprovals.Remove(row);
                row.Dispose();
                UpdateApprovalsBadge();
            });
        }
    };
    PendingApprovals.Add(row);
    UpdateApprovalsBadge();
}

private void UpdateApprovalsBadge()
{
    ApprovalsBadgeText = PendingApprovals.Count == 0
        ? "Sin pendientes"
        : $"{PendingApprovals.Count} pendiente(s)";
}
```

- [ ] **Step 5: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded (habrá errores de DI hasta Task 3).

- [ ] **Step 6: Commit**

```
git add src/GUI/ViewModels/AccessControlViewModel.cs
git commit -m "feat(gui): AccessControlViewModel suscribe a IApprovalQueue para cola en vivo"
```

---

### Task 3: Actualizar el registro de DI

**Files:**
- Modify: `src/GUI/Bootstrap/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Reemplazar el registro de `AccessControlViewModel`**

Localizar el bloque actual:
```csharp
services.AddTransient<AccessControlViewModel>(sp =>
    new AccessControlViewModel(
        sp.GetRequiredService<IParkingModeService>(),
        sp.GetRequiredService<IAccessPolicyConfigService>(),
        sp.GetRequiredService<ILotSnapshotStream>(),
        sp.GetRequiredService<IUiThreadDispatcher>()));
```

Reemplazarlo por:
```csharp
services.AddTransient<AccessControlViewModel>(sp =>
    new AccessControlViewModel(
        sp.GetRequiredService<IParkingModeService>(),
        sp.GetRequiredService<IAccessPolicyConfigService>(),
        sp.GetRequiredService<ILotSnapshotStream>(),
        sp.GetRequiredService<IUiThreadDispatcher>(),
        sp.GetRequiredService<IApprovalQueue>(),
        sp.GetRequiredService<IApprovalDecisionService>(),
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
```

- [ ] **Step 2: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/GUI/Bootstrap/ServiceCollectionExtensions.cs
git commit -m "feat(di): AccessControlViewModel recibe IApprovalQueue e IApprovalDecisionService"
```

---

### Task 4: Reemplazar el placeholder de aprobaciones en `AdminPage.xaml`

**Files:**
- Modify: `src/GUI/Pages/AdminPage.xaml`

- [ ] **Step 1: Localizar el placeholder de F2 y reemplazarlo**

Localizar el bloque con `x:Name="ApprovalsSection"`:
```xml
<!-- Cola de aprobaciones (placeholder para F3) -->
<Border Style="{StaticResource CardStyle}" Padding="20" x:Name="ApprovalsSection">
    <StackPanel Spacing="8">
        <TextBlock Text="APROBACIONES PENDIENTES"
                   FontSize="10" FontWeight="SemiBold" CharacterSpacing="60"
                   Foreground="{ThemeResource Tx3Brush}" />
        <TextBlock Text="— Se implementa en F3 —"
                   FontSize="12" Foreground="{ThemeResource Tx3Brush}" />
    </StackPanel>
</Border>
```

Reemplazarlo por:
```xml
<!-- Cola de aprobaciones en vivo -->
<Border Style="{StaticResource CardStyle}" Padding="20">
    <StackPanel Spacing="12">
        <StackPanel Orientation="Horizontal" Spacing="10">
            <TextBlock Text="APROBACIONES PENDIENTES"
                       FontSize="10" FontWeight="SemiBold" CharacterSpacing="60"
                       Foreground="{ThemeResource Tx3Brush}"
                       VerticalAlignment="Center" />
            <Border CornerRadius="10" Padding="8,2"
                    Background="{ThemeResource AccentFillColorDefaultBrush}">
                <TextBlock Text="{x:Bind ViewModel.ApprovalsBadgeText, Mode=OneWay}"
                           FontSize="11" FontWeight="SemiBold"
                           Foreground="White" />
            </Border>
        </StackPanel>

        <TextBlock Text="Sin aprobaciones pendientes — el modo actual no requiere aprobación manual."
                   FontSize="12" Foreground="{ThemeResource Tx3Brush}"
                   Visibility="{x:Bind ViewModel.PendingApprovals.Count, Mode=OneWay, Converter={StaticResource IntToVisibilityConverter}}" />

        <ItemsRepeater ItemsSource="{x:Bind ViewModel.PendingApprovals, Mode=OneWay}">
            <ItemsRepeater.ItemTemplate>
                <DataTemplate x:DataType="vm:PendingApprovalRowVm">
                    <Grid Padding="0,6" ColumnSpacing="12"
                          BorderBrush="{ThemeResource Stroke2Brush}"
                          BorderThickness="0,0,0,1">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="120" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="100" />
                            <ColumnDefinition Width="80" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>

                        <TextBlock Grid.Column="0"
                                   Text="{x:Bind Id}"
                                   FontFamily="Cascadia Code, Consolas, monospace"
                                   FontSize="11" VerticalAlignment="Center"
                                   Foreground="{ThemeResource Tx3Brush}" />
                        <TextBlock Grid.Column="1"
                                   Text="{x:Bind Plate}"
                                   FontSize="13" FontWeight="SemiBold"
                                   VerticalAlignment="Center"
                                   Foreground="{ThemeResource Tx1Brush}" />
                        <TextBlock Grid.Column="2"
                                   Text="{x:Bind GateId}"
                                   FontSize="12" VerticalAlignment="Center"
                                   Foreground="{ThemeResource Tx2Brush}" />
                        <TextBlock Grid.Column="3"
                                   Text="{x:Bind ElapsedLabel, Mode=OneWay}"
                                   FontSize="11" VerticalAlignment="Center"
                                   Foreground="{ThemeResource Tx3Brush}" />

                        <StackPanel Grid.Column="4" Orientation="Horizontal" Spacing="6">
                            <Button Command="{x:Bind ApproveCommand}"
                                    Style="{StaticResource AccentButtonStyle}"
                                    AutomationProperties.Name="Aprobar">
                                <StackPanel Orientation="Horizontal" Spacing="4">
                                    <FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE73E;"
                                              FontSize="12" />
                                    <TextBlock Text="Aprobar" FontSize="12" />
                                </StackPanel>
                            </Button>
                            <Button Command="{x:Bind DenyCommand}"
                                    AutomationProperties.Name="Denegar">
                                <StackPanel Orientation="Horizontal" Spacing="4">
                                    <FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE711;"
                                              FontSize="12" />
                                    <TextBlock Text="Denegar" FontSize="12" />
                                </StackPanel>
                            </Button>
                        </StackPanel>
                    </Grid>
                </DataTemplate>
            </ItemsRepeater.ItemTemplate>
        </ItemsRepeater>
    </StackPanel>
</Border>
```

- [ ] **Step 2: Añadir `IntToVisibilityConverter` si no existe**

Verificar si `IntToVisibilityConverter` está registrado en los recursos:
```
grep -r "IntToVisibilityConverter" src/GUI/
```

Si no existe, crear `src/GUI/Converters/IntToVisibilityConverter.cs`:
```csharp
// src/GUI/Converters/IntToVisibilityConverter.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SmartParkingLot.Gui.Converters;

public sealed class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
```

Registrar en `App.xaml`:
```xml
<converters:IntToVisibilityConverter x:Key="IntToVisibilityConverter" />
```

- [ ] **Step 3: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 4: Commit**

```
git add src/GUI/Pages/AdminPage.xaml src/GUI/Converters/
git commit -m "feat(gui): sección de aprobaciones pendientes en vivo en Control de Acceso"
```

---

### Task 5: Prueba funcional end-to-end de F3

- [ ] **Step 1: Arrancar la app**

```
dotnet run --project src/GUI/SmartParkingLot.Gui.csproj
```

- [ ] **Step 2: Activar modo MANUAL**

En "Control de Acceso", seleccionar "Manual (aprobación operario)". Verificar:
- Badge cambia a naranja "MANUAL".
- Panel de aprobaciones muestra "Sin aprobaciones pendientes".

- [ ] **Step 3: Simular una solicitud de entrada**

Ir a la página "Mapa de Spots" y pulsar el botón "Entrada" (simula un vehículo pidiendo entrar). Volver a "Control de Acceso".

Verificar:
- Aparece una fila con Id (APR-XXXXXXXX), placa simulada, gate y contador de segundos en aumento.
- El badge del contador cambia a "1 pendiente(s)".

- [ ] **Step 4: Aprobar la solicitud**

Pulsar "Aprobar" en la fila. Verificar:
- La fila desaparece de la lista.
- El badge vuelve a "Sin pendientes".
- La barrera de entrada reacciona (si Arduino está conectado) o el log refleja "APROBADA por operario".

- [ ] **Step 5: Probar el flujo de denegación**

Repetir Step 3 y pulsar "Denegar". Verificar que el log refleja "DENEGADA por operario".

- [ ] **Step 6: Probar el timeout automático**

Activar MANUAL, simular entrada y esperar el tiempo configurado en `ManualApprovalTimeoutSeconds` (por defecto 30s) sin aprobar ni denegar. Verificar que el log refleja "DENEGADA por timeout" y la fila desaparece.

- [ ] **Step 7: Commit final de F3**

```
git add -A
git commit -m "feat(f3): cola de aprobaciones en vivo completa — modo MANUAL end-to-end funcional"
```

---

### Task 6: Limpieza final

**Files:**
- Delete content of: `src/GUI/ViewModels/AdminPageViewModel.cs`

- [ ] **Step 1: Eliminar el archivo `AdminPageViewModel.cs`**

```
git rm src/GUI/ViewModels/AdminPageViewModel.cs
```

Verificar que nada importa `AdminPageViewModel` o `SpotAdminRowVm`:
```
grep -r "AdminPageViewModel\|SpotAdminRowVm" src/
```
Esperado: 0 resultados.

- [ ] **Step 2: Compilar**

```
dotnet build
```
Esperado: Build succeeded.

- [ ] **Step 3: Ejecutar todos los tests**

```
dotnet test tests/SmartParkingLot.Tests -v minimal
```
Esperado: todos pasan.

- [ ] **Step 4: Commit final**

```
git add -A
git commit -m "chore(gui): eliminar AdminPageViewModel obsoleto tras reconversión completa"
```
