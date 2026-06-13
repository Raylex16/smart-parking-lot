# F2: Página "Control de Acceso" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconvertir la página "Gestión de Spots" en "Control de Acceso": selector de política activa (4 modos), panel de parámetros condicional (whitelist de placas y rango horario), y cableado completo al backend de F1.

**Architecture:** Se elimina `AdminPageViewModel` + `SpotAdminRowVm` y se crea `AccessControlViewModel` en su lugar. El XAML se reescribe completamente. `ServiceCollectionExtensions` y `MainWindow` se actualizan para referenciar el nuevo VM y el nuevo tag de navegación.

**Tech Stack:** WinUI 3, CommunityToolkit.Mvvm, `x:Bind` con `Mode=TwoWay`/`OneWay`.

**Prerequisito:** F1 debe estar completa y compilando (`IAccessPolicyConfigService`, `IParkingModeService` registrados en DI).

---

## Mapa de archivos

| Acción | Archivo |
|--------|---------|
| Delete | `src/GUI/ViewModels/AdminPageViewModel.cs` (contenido reemplazado) |
| Create | `src/GUI/ViewModels/AccessControlViewModel.cs` |
| Create | `src/GUI/ViewModels/PlateRowVm.cs` |
| Modify | `src/GUI/Pages/AdminPage.xaml` |
| Modify | `src/GUI/Pages/AdminPage.xaml.cs` |
| Modify | `src/GUI/Bootstrap/ServiceCollectionExtensions.cs` |
| Modify | `src/GUI/MainWindow.xaml` |
| Modify | `src/GUI/MainWindow.xaml.cs` |

---

### Task 1: Eliminar `AdminPageViewModel` y crear `PlateRowVm`

**Files:**
- Delete content of: `src/GUI/ViewModels/AdminPageViewModel.cs`
- Create: `src/GUI/ViewModels/PlateRowVm.cs`

- [ ] **Step 1: Vaciar `AdminPageViewModel.cs` y convertirlo en alias temporal**

Reemplazar todo el contenido de `src/GUI/ViewModels/AdminPageViewModel.cs` con una clase vacía para que el proyecto compile mientras se crea el nuevo VM:

```csharp
// src/GUI/ViewModels/AdminPageViewModel.cs
// Archivo obsoleto — será eliminado al final de F2.
// Mantenido temporalmente para evitar errores de compilación en code-behind.
namespace SmartParkingLot.Gui.ViewModels;

[Obsolete("Reemplazado por AccessControlViewModel")]
public sealed class AdminPageViewModel { }
public sealed class SpotAdminRowVm
{
    public string Id         { get; set; } = "";
    public string Address    { get; set; } = "";
    public string Type       { get; set; } = "";
    public string Floor      { get; set; } = "";
    public bool   IsOccupied { get; set; }
    public string StateLabel => IsOccupied ? "Ocupado" : "Libre";
}
```

- [ ] **Step 2: Crear `PlateRowVm`**

```csharp
// src/GUI/ViewModels/PlateRowVm.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class PlateRowVm : ObservableObject
{
    [ObservableProperty] private string _plate = "";

    public IRelayCommand<PlateRowVm>? RemoveCommand { get; set; }
}
```

- [ ] **Step 3: Compilar para verificar que no hay errores de compilación**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 4: Commit**

```
git add src/GUI/ViewModels/AdminPageViewModel.cs src/GUI/ViewModels/PlateRowVm.cs
git commit -m "refactor(gui): vaciar AdminPageViewModel obsoleto, crear PlateRowVm"
```

---

### Task 2: Crear `AccessControlViewModel`

**Files:**
- Create: `src/GUI/ViewModels/AccessControlViewModel.cs`

- [ ] **Step 1: Crear el ViewModel**

```csharp
// src/GUI/ViewModels/AccessControlViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Infrastructure;
using SmartParkingLot.Gui.Resources;

namespace SmartParkingLot.Gui.ViewModels;

public partial class AccessControlViewModel : ObservableObject
{
    private readonly IParkingModeService       _modeService;
    private readonly IAccessPolicyConfigService _configService;
    private readonly ILotSnapshotStream        _stream;
    private readonly IUiThreadDispatcher       _ui;

    [ObservableProperty] private int    _selectedModeIndex;
    [ObservableProperty] private string _policyBadgeText  = "";
    [ObservableProperty] private SolidColorBrush _policyBadgeColor = new(Colors.Green);
    [ObservableProperty] private bool   _showWhitelistPanel;
    [ObservableProperty] private bool   _showSchedulePanel;
    [ObservableProperty] private string _newPlate         = "";
    [ObservableProperty] private TimeSpan _scheduleStart  = TimeSpan.FromHours(8);
    [ObservableProperty] private TimeSpan _scheduleEnd    = TimeSpan.FromHours(20);
    [ObservableProperty] private string _statusMessage    = "";
    [ObservableProperty] private bool   _whitelistIsEmpty;

    public ObservableCollection<PlateRowVm> Plates { get; } = new();

    private static readonly ParkingMode[] ModeIndexMap =
    [
        ParkingMode.AUTOMATIC,
        ParkingMode.MANUAL,
        ParkingMode.RESTRICTED,
        ParkingMode.SCHEDULED
    ];

    public AccessControlViewModel(
        IParkingModeService modeService,
        IAccessPolicyConfigService configService,
        ILotSnapshotStream stream,
        IUiThreadDispatcher ui)
    {
        _modeService   = modeService;
        _configService = configService;
        _stream        = stream;
        _ui            = ui;
    }

    public void Activate() => _ = LoadAsync();
    public void Deactivate() { }

    private async Task LoadAsync()
    {
        var lotId  = _stream.Current.Id;
        var config = await _configService.GetAsync(lotId);

        SelectedModeIndex = Array.IndexOf(ModeIndexMap, _modeService.Current);
        UpdatePanelVisibility(_modeService.Current);
        UpdateBadge(_modeService.Current);

        Plates.Clear();
        foreach (var p in config.AllowedPlates)
            Plates.Add(BuildPlateRow(p));

        ScheduleStart = config.ScheduleStart;
        ScheduleEnd   = config.ScheduleEnd;
        WhitelistIsEmpty = Plates.Count == 0;
    }

    [RelayCommand]
    private async Task SwitchPolicyAsync(int modeIndex)
    {
        if (modeIndex < 0 || modeIndex >= ModeIndexMap.Length) return;
        var mode = ModeIndexMap[modeIndex];
        await _modeService.SwitchToAsync(mode);
        UpdatePanelVisibility(mode);
        UpdateBadge(mode);
        StatusMessage = $"Política cambiada a {ModeLabel(mode)}";
    }

    [RelayCommand]
    private void AddPlate()
    {
        var plate = NewPlate.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(plate)) return;
        if (Plates.Any(r => r.Plate.Equals(plate, StringComparison.OrdinalIgnoreCase))) return;
        Plates.Add(BuildPlateRow(plate));
        NewPlate = "";
        WhitelistIsEmpty = false;
    }

    [RelayCommand]
    private async Task SaveWhitelistAsync()
    {
        var lotId = _stream.Current.Id;
        var plates = Plates.Select(r => r.Plate).ToList();
        await _configService.UpdateWhitelistAsync(lotId, plates);
        WhitelistIsEmpty = plates.Count == 0;
        StatusMessage = $"Whitelist guardada ({plates.Count} placa(s))";
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (ScheduleEnd <= ScheduleStart)
        {
            StatusMessage = "Error: la hora de fin debe ser posterior a la de inicio.";
            return;
        }
        var lotId = _stream.Current.Id;
        await _configService.UpdateScheduleAsync(lotId, ScheduleStart, ScheduleEnd);
        StatusMessage = $"Horario guardado ({ScheduleStart:hh\\:mm}–{ScheduleEnd:hh\\:mm})";
    }

    private void RemovePlate(PlateRowVm row)
    {
        Plates.Remove(row);
        WhitelistIsEmpty = Plates.Count == 0;
    }

    private PlateRowVm BuildPlateRow(string plate)
    {
        var row = new PlateRowVm { Plate = plate };
        row.RemoveCommand = new RelayCommand<PlateRowVm>(r => { if (r is not null) RemovePlate(r); });
        return row;
    }

    partial void OnSelectedModeIndexChanged(int value)
        => _ = SwitchPolicyAsync(value);

    private void UpdatePanelVisibility(ParkingMode mode)
    {
        ShowWhitelistPanel = mode == ParkingMode.RESTRICTED;
        ShowSchedulePanel  = mode == ParkingMode.SCHEDULED;
    }

    private void UpdateBadge(ParkingMode mode)
    {
        (PolicyBadgeText, PolicyBadgeColor) = mode switch
        {
            ParkingMode.AUTOMATIC  => ("AUTOMÁTICO",  new SolidColorBrush(Colors.Green)),
            ParkingMode.MANUAL     => ("MANUAL",       new SolidColorBrush(Colors.Orange)),
            ParkingMode.RESTRICTED => ("RESTRINGIDO",  new SolidColorBrush(Colors.Red)),
            ParkingMode.SCHEDULED  => ("PROGRAMADO",   new SolidColorBrush(Colors.SteelBlue)),
            _                      => ("DESCONOCIDO",  new SolidColorBrush(Colors.Gray))
        };
    }

    private static string ModeLabel(ParkingMode mode) => mode switch
    {
        ParkingMode.AUTOMATIC  => "Automático",
        ParkingMode.MANUAL     => "Manual",
        ParkingMode.RESTRICTED => "Restringido",
        ParkingMode.SCHEDULED  => "Programado",
        _                      => mode.ToString()
    };
}
```

- [ ] **Step 2: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/GUI/ViewModels/AccessControlViewModel.cs
git commit -m "feat(gui): AccessControlViewModel — selector de política + whitelist + horario"
```

---

### Task 3: Reescribir `AdminPage.xaml`

**Files:**
- Modify: `src/GUI/Pages/AdminPage.xaml`

- [ ] **Step 1: Reemplazar todo el contenido de `AdminPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Page
    x:Class="SmartParkingLot.Gui.Pages.AdminPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:SmartParkingLot.Gui.ViewModels">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Header -->
        <Grid Grid.Row="0" Height="56" Padding="20,0"
              Background="{ThemeResource Layer1Brush}"
              BorderBrush="{ThemeResource StrokeBrush}"
              BorderThickness="0,0,0,1">
            <StackPanel VerticalAlignment="Center">
                <TextBlock Text="Control de Acceso"
                           FontSize="15" FontWeight="SemiBold"
                           Foreground="{ThemeResource Tx1Brush}" />
                <TextBlock Text="Política activa y parámetros de acceso al parqueadero"
                           FontSize="11" Foreground="{ThemeResource Tx3Brush}" />
            </StackPanel>
        </Grid>

        <!-- Contenido principal -->
        <ScrollViewer Grid.Row="1" Padding="20" VerticalScrollBarVisibility="Auto">
            <StackPanel Spacing="16">

                <!-- Sección: Política activa -->
                <Border Style="{StaticResource CardStyle}" Padding="20">
                    <StackPanel Spacing="12">
                        <TextBlock Text="POLÍTICA ACTIVA"
                                   FontSize="10" FontWeight="SemiBold" CharacterSpacing="60"
                                   Foreground="{ThemeResource Tx3Brush}" />

                        <StackPanel Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
                            <ComboBox SelectedIndex="{x:Bind ViewModel.SelectedModeIndex, Mode=TwoWay}"
                                      MinWidth="200">
                                <ComboBoxItem Content="Automático" />
                                <ComboBoxItem Content="Manual (aprobación operario)" />
                                <ComboBoxItem Content="Restringido (whitelist)" />
                                <ComboBoxItem Content="Programado (horario)" />
                            </ComboBox>
                            <Border CornerRadius="4" Padding="10,4"
                                    Background="{x:Bind ViewModel.PolicyBadgeColor, Mode=OneWay}">
                                <TextBlock Text="{x:Bind ViewModel.PolicyBadgeText, Mode=OneWay}"
                                           FontSize="11" FontWeight="SemiBold"
                                           Foreground="White" />
                            </Border>
                        </StackPanel>

                        <TextBlock Text="{x:Bind ViewModel.StatusMessage, Mode=OneWay}"
                                   FontSize="12" Foreground="{ThemeResource Tx3Brush}"
                                   Visibility="{x:Bind ViewModel.StatusMessage.Length, Mode=OneWay, Converter={StaticResource IntToVisibilityConverter}}" />
                    </StackPanel>
                </Border>

                <!-- Panel: Whitelist (RESTRICTED) -->
                <Border Style="{StaticResource CardStyle}" Padding="20"
                        Visibility="{x:Bind ViewModel.ShowWhitelistPanel, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
                    <StackPanel Spacing="12">
                        <TextBlock Text="PLACAS PERMITIDAS"
                                   FontSize="10" FontWeight="SemiBold" CharacterSpacing="60"
                                   Foreground="{ThemeResource Tx3Brush}" />

                        <InfoBar IsOpen="{x:Bind ViewModel.WhitelistIsEmpty, Mode=OneWay}"
                                 Severity="Warning"
                                 Title="Whitelist vacía"
                                 Message="Ningún vehículo podrá ingresar mientras la whitelist esté vacía." />

                        <!-- Lista de placas -->
                        <ItemsRepeater ItemsSource="{x:Bind ViewModel.Plates, Mode=OneWay}">
                            <ItemsRepeater.ItemTemplate>
                                <DataTemplate x:DataType="vm:PlateRowVm">
                                    <Grid Padding="0,4" ColumnSpacing="8">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*" />
                                            <ColumnDefinition Width="Auto" />
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Text="{x:Bind Plate}"
                                                   FontFamily="Cascadia Code, Consolas, monospace"
                                                   FontSize="13" VerticalAlignment="Center"
                                                   Foreground="{ThemeResource Tx1Brush}" />
                                        <Button Grid.Column="1"
                                                Command="{x:Bind RemoveCommand}"
                                                CommandParameter="{x:Bind}"
                                                AutomationProperties.Name="Eliminar placa">
                                            <FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE711;"
                                                      FontSize="12" />
                                        </Button>
                                    </Grid>
                                </DataTemplate>
                            </ItemsRepeater.ItemTemplate>
                        </ItemsRepeater>

                        <!-- Añadir nueva placa -->
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <TextBox Text="{x:Bind ViewModel.NewPlate, Mode=TwoWay}"
                                     PlaceholderText="Ej: ABC-123"
                                     MinWidth="160" />
                            <Button Command="{x:Bind ViewModel.AddPlateCommand}"
                                    AutomationProperties.Name="Añadir placa">
                                <StackPanel Orientation="Horizontal" Spacing="6">
                                    <FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE710;" FontSize="12" />
                                    <TextBlock Text="Añadir" />
                                </StackPanel>
                            </Button>
                        </StackPanel>

                        <Button Command="{x:Bind ViewModel.SaveWhitelistCommand}" Style="{StaticResource AccentButtonStyle}">
                            <TextBlock Text="Guardar whitelist" />
                        </Button>
                    </StackPanel>
                </Border>

                <!-- Panel: Horario (SCHEDULED) -->
                <Border Style="{StaticResource CardStyle}" Padding="20"
                        Visibility="{x:Bind ViewModel.ShowSchedulePanel, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
                    <StackPanel Spacing="12">
                        <TextBlock Text="HORARIO DE ACCESO"
                                   FontSize="10" FontWeight="SemiBold" CharacterSpacing="60"
                                   Foreground="{ThemeResource Tx3Brush}" />

                        <Grid ColumnSpacing="16">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto" />
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>
                            <StackPanel Spacing="4">
                                <TextBlock Text="Hora de inicio" FontSize="12"
                                           Foreground="{ThemeResource Tx2Brush}" />
                                <TimePicker SelectedTime="{x:Bind ViewModel.ScheduleStart, Mode=TwoWay}"
                                            ClockIdentifier="24HourClock" />
                            </StackPanel>
                            <StackPanel Grid.Column="1" Spacing="4">
                                <TextBlock Text="Hora de fin" FontSize="12"
                                           Foreground="{ThemeResource Tx2Brush}" />
                                <TimePicker SelectedTime="{x:Bind ViewModel.ScheduleEnd, Mode=TwoWay}"
                                            ClockIdentifier="24HourClock" />
                            </StackPanel>
                        </Grid>

                        <Button Command="{x:Bind ViewModel.SaveScheduleCommand}" Style="{StaticResource AccentButtonStyle}">
                            <TextBlock Text="Guardar horario" />
                        </Button>
                    </StackPanel>
                </Border>

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

            </StackPanel>
        </ScrollViewer>
    </Grid>
</Page>
```

- [ ] **Step 2: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded (el code-behind sigue referenciando `AdminPageViewModel` temporalmente; lo cambiamos en el Task 4).

- [ ] **Step 3: Commit**

```
git add src/GUI/Pages/AdminPage.xaml
git commit -m "feat(gui): AdminPage.xaml reescrito como Control de Acceso"
```

---

### Task 4: Actualizar `AdminPage.xaml.cs`

**Files:**
- Modify: `src/GUI/Pages/AdminPage.xaml.cs`

- [ ] **Step 1: Reemplazar el code-behind**

```csharp
// src/GUI/Pages/AdminPage.xaml.cs
using Microsoft.UI.Xaml.Controls;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class AdminPage : Page
{
    public AccessControlViewModel ViewModel { get; }

    public AdminPage(AccessControlViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Loaded   += (_, _) => ViewModel.Activate();
        Unloaded += (_, _) => ViewModel.Deactivate();
    }
}
```

- [ ] **Step 2: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/GUI/Pages/AdminPage.xaml.cs
git commit -m "refactor(gui): AdminPage code-behind usa AccessControlViewModel"
```

---

### Task 5: Actualizar `ServiceCollectionExtensions.cs`

**Files:**
- Modify: `src/GUI/Bootstrap/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Reemplazar el registro de `AdminPageViewModel` por `AccessControlViewModel`**

Localizar y reemplazar el bloque (aprox. líneas 90–93):
```csharp
services.AddTransient<AdminPageViewModel>(sp =>
    new AdminPageViewModel(
        sp.GetRequiredService<IGetSpotRowsQuery>(),
        sp.GetRequiredService<ILotSnapshotStream>()));
```

Por:
```csharp
services.AddTransient<AccessControlViewModel>(sp =>
    new AccessControlViewModel(
        sp.GetRequiredService<IParkingModeService>(),
        sp.GetRequiredService<IAccessPolicyConfigService>(),
        sp.GetRequiredService<ILotSnapshotStream>(),
        sp.GetRequiredService<IUiThreadDispatcher>()));
```

Localizar y reemplazar el registro de `AdminPage` (aprox. línea 105–107):
```csharp
services.AddTransient<Pages.AdminPage>(sp =>
    new Pages.AdminPage(sp.GetRequiredService<AdminPageViewModel>()));
```

Por:
```csharp
services.AddTransient<Pages.AdminPage>(sp =>
    new Pages.AdminPage(sp.GetRequiredService<AccessControlViewModel>()));
```

Añadir los usings necesarios al inicio del archivo:
```csharp
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Application.Services;
```

- [ ] **Step 2: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/GUI/Bootstrap/ServiceCollectionExtensions.cs
git commit -m "feat(di): registrar AccessControlViewModel; retirar AdminPageViewModel del contenedor"
```

---

### Task 6: Actualizar navegación en `MainWindow`

**Files:**
- Modify: `src/GUI/MainWindow.xaml`
- Modify: `src/GUI/MainWindow.xaml.cs`

- [ ] **Step 1: Actualizar el `NavigationViewItem` en `MainWindow.xaml`**

Localizar el item con `Tag="admin"`:
```xml
<NavigationViewItem Tag="admin" Content="Gestión de Spots">
    <NavigationViewItem.Icon>
        <SymbolIcon Symbol="ContactInfo" />
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

Reemplazarlo por:
```xml
<NavigationViewItem Tag="access" Content="Control de Acceso">
    <NavigationViewItem.Icon>
        <FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE8D7;" />
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

- [ ] **Step 2: Actualizar el switch en `MainWindow.xaml.cs`**

Localizar dentro de `Nav_SelectionChanged` la línea:
```csharp
"admin"     => App.Services.GetRequiredService<AdminPage>(),
```

Reemplazarla por:
```csharp
"access"    => App.Services.GetRequiredService<AdminPage>(),
```

- [ ] **Step 3: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 4: Commit**

```
git add src/GUI/MainWindow.xaml src/GUI/MainWindow.xaml.cs
git commit -m "feat(gui): renombrar nav item Admin → Control de Acceso (tag: access)"
```

---

### Task 7: Añadir convertidores de visibilidad faltantes en `App.xaml` / recursos

**Files:**
- Modify: `src/GUI/App.xaml` (o el archivo de recursos donde estén los convertidores)

- [ ] **Step 1: Verificar que `BoolToVisibilityConverter` existe en recursos**

```
grep -r "BoolToVisibilityConverter" src/GUI/
```
Si no existe, añadirlo en `src/GUI/Styles/Theme.xaml` o en el `ResourceDictionary` del `App.xaml`.

- [ ] **Step 2: Si `BoolToVisibilityConverter` no existe, añadirlo**

Crear `src/GUI/Converters/BoolToVisibilityConverter.cs`:
```csharp
// src/GUI/Converters/BoolToVisibilityConverter.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SmartParkingLot.Gui.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}
```

Registrar en `App.xaml` dentro del `ResourceDictionary` principal:
```xml
<converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
```

Con el xmlns:
```xml
xmlns:converters="using:SmartParkingLot.Gui.Converters"
```

- [ ] **Step 3: Compilar**

```
dotnet build src/GUI/SmartParkingLot.Gui.csproj
```
Esperado: Build succeeded.

- [ ] **Step 4: Commit**

```
git add src/GUI/
git commit -m "feat(gui): BoolToVisibilityConverter para paneles condicionales de Control de Acceso"
```

---

### Task 8: Prueba funcional de F2

- [ ] **Step 1: Arrancar la app**

```
dotnet run --project src/GUI/SmartParkingLot.Gui.csproj
```

- [ ] **Step 2: Navegar a "Control de Acceso"**

Verificar que:
- El `ComboBox` muestra el modo actual (debería ser AUTOMÁTICO por defecto).
- El badge verde muestra "AUTOMÁTICO".
- Los paneles de whitelist y horario están ocultos.

- [ ] **Step 3: Cambiar a RESTRICTED**

Seleccionar "Restringido (whitelist)" en el ComboBox. Verificar:
- Badge cambia a rojo "RESTRINGIDO".
- Panel de whitelist aparece.
- `InfoBar` de advertencia sobre whitelist vacía es visible.

- [ ] **Step 4: Añadir una placa y guardar**

Escribir "ABC-123" en el TextBox, pulsar Añadir, luego "Guardar whitelist". Verificar:
- La placa aparece en la lista.
- El mensaje de estado indica "Whitelist guardada (1 placa(s))".
- La `InfoBar` de advertencia desaparece.

- [ ] **Step 5: Reiniciar la app y volver a Control de Acceso**

```
dotnet run --project src/GUI/SmartParkingLot.Gui.csproj
```
Verificar que el modo sigue siendo RESTRICTED y la placa "ABC-123" sigue en la lista (persistencia SQLite).

- [ ] **Step 6: Cambiar a SCHEDULED y guardar horario**

Seleccionar "Programado (horario)", ajustar hora de inicio a 09:00 y fin a 18:00, pulsar "Guardar horario". Verificar:
- Mensaje de estado "Horario guardado (09:00–18:00)".
- Al reiniciar la app, el horario persiste.

- [ ] **Step 7: Commit final de F2**

```
git add -A
git commit -m "feat(f2): página Control de Acceso completa — selector + whitelist + horario"
```
