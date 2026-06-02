# Configuración de Hardware desde la GUI — Fase 1 (Plan de Implementación)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir editar `hardware.json` (puerto, baudRate, spots y gates) desde la pantalla Hardware/Arduino de la GUI, con validación y persistencia, aplicando los cambios al reiniciar; y consolidar los dos `hardware.json` divergentes en una única fuente de verdad.

**Architecture:** El `hardware.json` se consume en el bootstrap para construir el grafo de DI, por lo que la edición en caliente queda fuera de alcance: la Fase 1 escribe el archivo y pide reinicio. La lógica testeable (serialización y validación) vive en la capa Application (`SmartParkingLot.Application.Hardware`); la GUI solo aporta un ViewModel delgado y el XAML. Se crea un archivo canónico `config/hardware.json` en la raíz, enlazado por los proyectos GUI y CLI.

**Tech Stack:** C# 14 / .NET 10, WinUI 3 + CommunityToolkit.Mvvm, System.Text.Json, xUnit.

---

## Estructura de archivos

- `config/hardware.json` — **Crear**. Único archivo canónico de configuración (fuente de verdad), enlazado por GUI y CLI.
- `src/GUI/hardware.json` — **Eliminar**. Reemplazado por el enlace al canónico.
- `src/Cli/hardware.json` — **Eliminar**. Reemplazado por el enlace al canónico.
- `src/GUI/SmartParkingLot.Gui.csproj` — **Modificar**. Enlazar `..\..\config\hardware.json`.
- `src/Cli/SmartParkingLot.Cli.csproj` — **Modificar**. Enlazar `..\..\config\hardware.json`.
- `src/Application/Hardware/HardwareConfig.cs` — **Modificar**. Añadir `Save(path)`.
- `src/Application/Hardware/HardwareConfigValidator.cs` — **Crear**. Validación pura de un `HardwareConfig`.
- `src/GUI/ViewModels/HardwareConfigEditorViewModel.cs` — **Crear**. ViewModel del editor (colecciones editables, comando Guardar).
- `src/GUI/ViewModels/SpotMappingRowVm.cs` — **Crear**. Fila editable de spot.
- `src/GUI/ViewModels/GateMappingRowVm.cs` — **Crear**. Fila editable de gate.
- `src/GUI/Pages/HardwarePage.xaml` — **Modificar**. Añadir sección "Configuración".
- `src/GUI/Pages/HardwarePage.xaml.cs` — **Modificar**. Exponer el editor VM.
- `src/GUI/Bootstrap/ServiceCollectionExtensions.cs` — **Modificar**. Registrar el editor VM con la ruta del config.
- `tests/SmartParkingLot.Tests/HardwareConfigSaveTests.cs` — **Crear**. Round-trip de Save/Load.
- `tests/SmartParkingLot.Tests/HardwareConfigValidatorTests.cs` — **Crear**. Casos de validación.

---

## Task 1: Archivo canónico `config/hardware.json` + consolidación

**Files:**
- Create: `config/hardware.json`
- Modify: `src/GUI/SmartParkingLot.Gui.csproj:45-49`
- Modify: `src/Cli/SmartParkingLot.Cli.csproj:20-23`
- Delete: `src/GUI/hardware.json`, `src/Cli/hardware.json`

- [ ] **Step 1: Crear el archivo canónico**

Crear `config/hardware.json` con el set de 9 spots de la GUI (más rico y coincide con la BD/demo actual) más los campos que solo tenía el CLI (`manualApprovalTimeoutSeconds`, `allowedPlates`):

```json
{
  "port": "COM5",
  "baudRate": 115200,
  "manualApprovalTimeoutSeconds": 15,
  "allowedPlates": ["ABC-123", "XYZ-789"],
  "sensors": [
    { "sensorId": "IR1", "spotId": "A-01", "actuatorId": "LED1", "address": "Zona A, P. 1", "type": "Estándar", "floor": "Planta 1" },
    { "sensorId": "IR2", "spotId": "A-02", "actuatorId": "LED2", "address": "Zona A, P. 2", "type": "Estándar", "floor": "Planta 1" },
    { "sensorId": "IR3", "spotId": "A-03", "actuatorId": "LED3", "address": "Zona A, P. 3", "type": "Estándar", "floor": "Planta 1" },
    { "sensorId": "IR4", "spotId": "A-04", "actuatorId": "LED4", "address": "Zona A, P. 4", "type": "PMR", "floor": "Planta 1" },
    { "sensorId": "IR5", "spotId": "B-01", "actuatorId": "LED5", "address": "Zona B, P. 1", "type": "Estándar", "floor": "Planta 1" },
    { "sensorId": "IR6", "spotId": "B-02", "actuatorId": "LED6", "address": "Zona B, P. 2", "type": "Estándar", "floor": "Planta 1" },
    { "sensorId": "IR7", "spotId": "B-03", "actuatorId": "LED7", "address": "Zona B, P. 3", "type": "Moto", "floor": "Planta 1" },
    { "sensorId": "IR8", "spotId": "C-01", "actuatorId": "LED8", "address": "Zona C, P. 1", "type": "Estándar", "floor": "Planta 1" },
    { "sensorId": "IR9", "spotId": "C-02", "actuatorId": "LED9", "address": "Zona C, P. 2", "type": "Estándar", "floor": "Planta 1" }
  ],
  "gates": [
    { "gateId": "G-01", "type": "ENTRY", "irSensorId": "GATE-IR1", "actuatorId": "GATE1", "pin": 9 },
    { "gateId": "G-02", "type": "EXIT", "irSensorId": "GATE-IR2", "actuatorId": "GATE2", "pin": 10 }
  ],
  "cameras": [
    { "gateId": "G-01", "sensorId": "CAM-01" },
    { "gateId": "G-02", "sensorId": "CAM-02" }
  ]
}
```

- [ ] **Step 2: Enlazar el canónico desde la GUI**

En `src/GUI/SmartParkingLot.Gui.csproj`, reemplazar el bloque:

```xml
  <ItemGroup>
    <None Update="hardware.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

por:

```xml
  <ItemGroup>
    <None Include="..\..\config\hardware.json" Link="hardware.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 3: Enlazar el canónico desde el CLI**

En `src/Cli/SmartParkingLot.Cli.csproj`, reemplazar el bloque:

```xml
  <ItemGroup>
    <Content Include="hardware.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

por:

```xml
  <ItemGroup>
    <Content Include="..\..\config\hardware.json" Link="hardware.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

- [ ] **Step 4: Eliminar los archivos duplicados**

```bash
git rm src/GUI/hardware.json src/Cli/hardware.json
```

- [ ] **Step 5: Verificar que el build copia el canónico**

Run: `dotnet build smart-parking-lot.sln -c Debug`
Expected: `Compilación correcta`. Confirmar que `src/GUI/bin/Debug/net10.0-windows10.0.19041.0/win-x64/hardware.json` existe y tiene 9 spots.

- [ ] **Step 6: Commit**

```bash
git add config/hardware.json src/GUI/SmartParkingLot.Gui.csproj src/Cli/SmartParkingLot.Cli.csproj
git commit -m "refactor(config): consolidar hardware.json en un único archivo canónico"
```

---

## Task 2: `HardwareConfig.Save(path)`

**Files:**
- Modify: `src/Application/Hardware/HardwareConfig.cs:35-48`
- Test: `tests/SmartParkingLot.Tests/HardwareConfigSaveTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/SmartParkingLot.Tests/HardwareConfigSaveTests.cs`:

```csharp
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core;

namespace SmartParkingLot.Tests;

public class HardwareConfigSaveTests
{
    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var config = new HardwareConfig(
            Port: "COM7",
            BaudRate: 9600,
            Sensors: new[]
            {
                new SensorMapping("IR1", "A-01", "LED1", "Zona A, P. 1", "Estándar", "Planta 1")
            },
            Gates: new[]
            {
                new GateMapping("G-01", GateType.ENTRY, "GATE-IR1", "GATE1", 9)
            },
            ManualApprovalTimeoutSeconds: 20,
            AllowedPlates: new[] { "ABC-123" },
            Cameras: null);

        var path = Path.Combine(Path.GetTempPath(), $"hw-{Guid.NewGuid():N}.json");
        try
        {
            config.Save(path);
            var loaded = HardwareConfig.Load(path);

            Assert.Equal("COM7", loaded.Port);
            Assert.Equal(9600, loaded.BaudRate);
            Assert.Equal(20, loaded.ManualApprovalTimeoutSeconds);
            Assert.Single(loaded.Sensors);
            Assert.Equal("A-01", loaded.Sensors[0].SpotId);
            Assert.Single(loaded.Gates);
            Assert.Equal(GateType.ENTRY, loaded.Gates[0].Type);
            Assert.Equal(9, loaded.Gates[0].Pin);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Ejecutar el test para verificar que falla**

Run: `dotnet test tests/SmartParkingLot.Tests --filter HardwareConfigSaveTests`
Expected: FAIL con error de compilación "'HardwareConfig' no contiene una definición para 'Save'".

- [ ] **Step 3: Implementar `Save`**

En `src/Application/Hardware/HardwareConfig.cs`, dentro del record `HardwareConfig`, justo después del método `Load`, añadir:

```csharp
    public void Save(string path)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(this, options);

        // Escritura atómica: archivo temporal + move para no corromper el config
        // si el proceso muere a mitad de la escritura.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
```

- [ ] **Step 4: Ejecutar el test para verificar que pasa**

Run: `dotnet test tests/SmartParkingLot.Tests --filter HardwareConfigSaveTests`
Expected: PASS (1 passed).

- [ ] **Step 5: Commit**

```bash
git add src/Application/Hardware/HardwareConfig.cs tests/SmartParkingLot.Tests/HardwareConfigSaveTests.cs
git commit -m "feat(config): añadir HardwareConfig.Save con escritura atómica"
```

---

## Task 3: `HardwareConfigValidator`

**Files:**
- Create: `src/Application/Hardware/HardwareConfigValidator.cs`
- Test: `tests/SmartParkingLot.Tests/HardwareConfigValidatorTests.cs`

- [ ] **Step 1: Escribir los tests que fallan**

Crear `tests/SmartParkingLot.Tests/HardwareConfigValidatorTests.cs`:

```csharp
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core;

namespace SmartParkingLot.Tests;

public class HardwareConfigValidatorTests
{
    private static HardwareConfig Valid() => new(
        Port: "COM5",
        BaudRate: 115200,
        Sensors: new[]
        {
            new SensorMapping("IR1", "A-01", "LED1", "Zona A", "Estándar", "Planta 1"),
            new SensorMapping("IR2", "A-02", "LED2", "Zona A", "Estándar", "Planta 1")
        },
        Gates: new[] { new GateMapping("G-01", GateType.ENTRY, "GATE-IR1", "GATE1", 9) });

    [Fact]
    public void Validate_ValidConfig_NoErrors()
    {
        Assert.Empty(HardwareConfigValidator.Validate(Valid()));
    }

    [Fact]
    public void Validate_DuplicateSpotId_ReportsError()
    {
        var dup = Valid() with
        {
            Sensors = new[]
            {
                new SensorMapping("IR1", "A-01", "LED1", "Zona A", "Estándar", "Planta 1"),
                new SensorMapping("IR2", "A-01", "LED2", "Zona A", "Estándar", "Planta 1")
            }
        };
        Assert.Contains(HardwareConfigValidator.Validate(dup), e => e.Contains("A-01"));
    }

    [Fact]
    public void Validate_EmptyPort_ReportsError()
    {
        var bad = Valid() with { Port = "" };
        Assert.Contains(HardwareConfigValidator.Validate(bad), e => e.Contains("puerto"));
    }

    [Fact]
    public void Validate_InvalidPin_ReportsError()
    {
        var bad = Valid() with
        {
            Gates = new[] { new GateMapping("G-01", GateType.ENTRY, "GATE-IR1", "GATE1", 999) }
        };
        Assert.Contains(HardwareConfigValidator.Validate(bad), e => e.Contains("Pin"));
    }
}
```

- [ ] **Step 2: Ejecutar para verificar que falla**

Run: `dotnet test tests/SmartParkingLot.Tests --filter HardwareConfigValidatorTests`
Expected: FAIL con "el nombre 'HardwareConfigValidator' no existe".

- [ ] **Step 3: Implementar el validador**

Crear `src/Application/Hardware/HardwareConfigValidator.cs`:

```csharp
namespace SmartParkingLot.Application.Hardware;

/// <summary>
/// Validación pura de una configuración de hardware antes de persistirla.
/// Devuelve la lista de errores; vacía significa válida.
/// </summary>
public static class HardwareConfigValidator
{
    public static IReadOnlyList<string> Validate(HardwareConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.Port))
            errors.Add("El puerto no puede estar vacío.");

        if (config.BaudRate <= 0)
            errors.Add("El baudRate debe ser mayor que cero.");

        var spotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sensorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in config.Sensors)
        {
            if (string.IsNullOrWhiteSpace(s.SpotId))
                errors.Add("Hay un spot con Id vacío.");
            else if (!spotIds.Add(s.SpotId))
                errors.Add($"SpotId duplicado: '{s.SpotId}'.");

            if (string.IsNullOrWhiteSpace(s.SensorId))
                errors.Add($"El spot '{s.SpotId}' no tiene SensorId.");
            else if (!sensorIds.Add(s.SensorId))
                errors.Add($"SensorId duplicado: '{s.SensorId}'.");
        }

        var gateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in config.Gates)
        {
            if (string.IsNullOrWhiteSpace(g.GateId))
                errors.Add("Hay un gate con Id vacío.");
            else if (!gateIds.Add(g.GateId))
                errors.Add($"GateId duplicado: '{g.GateId}'.");

            if (g.Pin is < 0 or > 255)
                errors.Add($"Pin inválido en gate '{g.GateId}': {g.Pin} (rango 0-255).");
        }

        return errors;
    }
}
```

- [ ] **Step 4: Ejecutar para verificar que pasa**

Run: `dotnet test tests/SmartParkingLot.Tests --filter HardwareConfigValidatorTests`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add src/Application/Hardware/HardwareConfigValidator.cs tests/SmartParkingLot.Tests/HardwareConfigValidatorTests.cs
git commit -m "feat(config): añadir HardwareConfigValidator con tests"
```

---

## Task 4: Filas editables (`SpotMappingRowVm`, `GateMappingRowVm`)

**Files:**
- Create: `src/GUI/ViewModels/SpotMappingRowVm.cs`
- Create: `src/GUI/ViewModels/GateMappingRowVm.cs`

Nota de diseño: cada fila expone su propio `RemoveCommand`, que el editor cablea al crearla (la fila no conoce la colección padre; el editor inyecta la acción de borrado). Esto permite usar `x:Bind` de forma consistente en el `DataTemplate`, cuyo `DataContext` es la fila.

- [ ] **Step 1: Crear `SpotMappingRowVm`**

Crear `src/GUI/ViewModels/SpotMappingRowVm.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class SpotMappingRowVm : ObservableObject
{
    [ObservableProperty] private string _sensorId = "";
    [ObservableProperty] private string _spotId = "";
    [ObservableProperty] private string _actuatorId = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _type = "Estándar";
    [ObservableProperty] private string _floor = "Planta 1";

    public IRelayCommand? RemoveCommand { get; set; }
}
```

- [ ] **Step 2: Crear `GateMappingRowVm`**

Crear `src/GUI/ViewModels/GateMappingRowVm.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartParkingLot.Gui.ViewModels;

public partial class GateMappingRowVm : ObservableObject
{
    [ObservableProperty] private string _gateId = "";
    [ObservableProperty] private string _type = "ENTRY";
    [ObservableProperty] private string _irSensorId = "";
    [ObservableProperty] private string _actuatorId = "";
    [ObservableProperty] private string _pin = "";

    public IRelayCommand? RemoveCommand { get; set; }
}
```

- [ ] **Step 3: Verificar compilación**

Run: `dotnet build src/GUI/SmartParkingLot.Gui.csproj -c Debug`
Expected: `Compilación correcta`.

- [ ] **Step 4: Commit**

```bash
git add src/GUI/ViewModels/SpotMappingRowVm.cs src/GUI/ViewModels/GateMappingRowVm.cs
git commit -m "feat(gui): añadir filas editables de spot y gate"
```

---

## Task 5: `HardwareConfigEditorViewModel`

**Files:**
- Create: `src/GUI/ViewModels/HardwareConfigEditorViewModel.cs`

- [ ] **Step 1: Crear el ViewModel del editor**

Crear `src/GUI/ViewModels/HardwareConfigEditorViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core;

namespace SmartParkingLot.Gui.ViewModels;

public partial class HardwareConfigEditorViewModel : ObservableObject
{
    private readonly HardwareConfig _current;
    private readonly string _configPath;

    [ObservableProperty] private string _port = "";
    [ObservableProperty] private string _baudRate = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _needsRestart;

    public ObservableCollection<SpotMappingRowVm> Spots { get; } = new();
    public ObservableCollection<GateMappingRowVm> Gates { get; } = new();

    public HardwareConfigEditorViewModel(HardwareConfig current, string configPath)
    {
        _current    = current;
        _configPath = configPath;
        LoadFrom(current);
    }

    private void LoadFrom(HardwareConfig config)
    {
        Port     = config.Port;
        BaudRate = config.BaudRate.ToString();

        Spots.Clear();
        foreach (var s in config.Sensors)
            Spots.Add(NewSpotRow(new SpotMappingRowVm
            {
                SensorId = s.SensorId, SpotId = s.SpotId, ActuatorId = s.ActuatorId,
                Address = s.Address, Type = s.Type, Floor = s.Floor
            }));

        Gates.Clear();
        foreach (var g in config.Gates)
            Gates.Add(NewGateRow(new GateMappingRowVm
            {
                GateId = g.GateId, Type = g.Type.ToString(),
                IrSensorId = g.IrSensorId, ActuatorId = g.ActuatorId, Pin = g.Pin.ToString()
            }));
    }

    private SpotMappingRowVm NewSpotRow(SpotMappingRowVm row)
    {
        row.RemoveCommand = new RelayCommand(() => Spots.Remove(row));
        return row;
    }

    private GateMappingRowVm NewGateRow(GateMappingRowVm row)
    {
        row.RemoveCommand = new RelayCommand(() => Gates.Remove(row));
        return row;
    }

    [RelayCommand]
    private void AddSpot() => Spots.Add(NewSpotRow(new SpotMappingRowVm { SpotId = "NUEVO" }));

    [RelayCommand]
    private void AddGate() => Gates.Add(NewGateRow(new GateMappingRowVm { GateId = "G-NN", Type = "ENTRY" }));

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(BaudRate, out var baud))
        {
            StatusMessage = "BaudRate inválido: debe ser un número.";
            return;
        }

        var config = _current with
        {
            Port = Port.Trim(),
            BaudRate = baud,
            Sensors = Spots.Select(s => new SensorMapping(
                s.SensorId.Trim(), s.SpotId.Trim(), s.ActuatorId.Trim(),
                s.Address.Trim(), s.Type.Trim(), s.Floor.Trim())).ToList(),
            Gates = Gates.Select(g => new GateMapping(
                g.GateId.Trim(),
                Enum.TryParse<GateType>(g.Type, ignoreCase: true, out var t) ? t : GateType.ENTRY,
                g.IrSensorId.Trim(), g.ActuatorId.Trim(),
                int.TryParse(g.Pin, out var pin) ? pin : -1)).ToList()
        };

        var errors = HardwareConfigValidator.Validate(config);
        if (errors.Count > 0)
        {
            StatusMessage = "No se guardó. " + string.Join(" ", errors);
            return;
        }

        config.Save(_configPath);
        NeedsRestart  = true;
        StatusMessage = "Configuración guardada. Reinicia la aplicación para aplicar los cambios.";
    }
}
```

- [ ] **Step 2: Verificar compilación**

Run: `dotnet build src/GUI/SmartParkingLot.Gui.csproj -c Debug`
Expected: `Compilación correcta`.

- [ ] **Step 3: Commit**

```bash
git add src/GUI/ViewModels/HardwareConfigEditorViewModel.cs
git commit -m "feat(gui): añadir HardwareConfigEditorViewModel"
```

---

## Task 6: Registrar el editor en DI

**Files:**
- Modify: `src/GUI/Bootstrap/ServiceCollectionExtensions.cs:75-83`

- [ ] **Step 1: Registrar el ViewModel con la ruta del config**

En `src/GUI/Bootstrap/ServiceCollectionExtensions.cs`, justo después del bloque que registra `HardwarePageViewModel` (la llamada `services.AddTransient<HardwarePageViewModel>(...)`), añadir:

```csharp
        services.AddTransient<HardwareConfigEditorViewModel>(sp =>
            new HardwareConfigEditorViewModel(
                sp.GetRequiredService<SmartParkingLot.Application.Hardware.HardwareConfig>(),
                configPath));
```

Nota: `configPath` ya está en alcance en `BuildParkingServiceProviderAsync` (`var configPath = Path.Combine(baseDir, "hardware.json");`) y `HardwareConfig` se registra como singleton en `ApplicationModule.AddSmartParkingApplicationServices`.

- [ ] **Step 2: Verificar compilación**

Run: `dotnet build src/GUI/SmartParkingLot.Gui.csproj -c Debug`
Expected: `Compilación correcta`.

- [ ] **Step 3: Commit**

```bash
git add src/GUI/Bootstrap/ServiceCollectionExtensions.cs
git commit -m "feat(gui): registrar HardwareConfigEditorViewModel en DI"
```

---

## Task 7: UI del editor en HardwarePage

**Files:**
- Modify: `src/GUI/Pages/HardwarePage.xaml.cs:10-23`
- Modify: `src/GUI/Pages/HardwarePage.xaml` (añadir sección dentro del `StackPanel` principal)

- [ ] **Step 1: Exponer el editor en el code-behind**

En `src/GUI/Pages/HardwarePage.xaml.cs`, modificar el constructor para recibir y exponer el editor. Reemplazar la propiedad y el constructor existentes por:

```csharp
    public HardwarePageViewModel ViewModel { get; }
    public HardwareConfigEditorViewModel Editor { get; }

    public HardwarePage(HardwarePageViewModel viewModel, HardwareConfigEditorViewModel editor)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Editor    = editor;
        Loaded   += (_, _) => ViewModel.Activate();
        Unloaded += (_, _) => ViewModel.Deactivate();

        ViewModel.LogLines.CollectionChanged += (_, _) =>
        {
            if (ViewModel.AutoScroll)
                LogScroll.ChangeView(null, double.MaxValue, null, true);
        };
    }
```

Además, en `src/GUI/Bootstrap/ServiceCollectionExtensions.cs`, actualizar el registro de la página para inyectar el editor. Reemplazar:

```csharp
        services.AddTransient<Pages.HardwarePage>(sp =>
            new Pages.HardwarePage(sp.GetRequiredService<HardwarePageViewModel>()));
```

por:

```csharp
        services.AddTransient<Pages.HardwarePage>(sp =>
            new Pages.HardwarePage(
                sp.GetRequiredService<HardwarePageViewModel>(),
                sp.GetRequiredService<HardwareConfigEditorViewModel>()));
```

- [ ] **Step 2: Añadir la sección de configuración al XAML**

En `src/GUI/Pages/HardwarePage.xaml`, dentro del `<StackPanel Spacing="12">` (línea 30), justo antes de su cierre `</StackPanel>` (línea 215), añadir:

```xml
                <!-- Configuración editable (Fase 1: aplica al reiniciar) -->
                <Border Style="{StaticResource CardStyle}">
                    <StackPanel Padding="16" Spacing="10">
                        <TextBlock Text="Configuración (hardware.json)"
                                   Style="{StaticResource CardTitleTextStyle}" />

                        <StackPanel Orientation="Horizontal" Spacing="12">
                            <StackPanel Spacing="4">
                                <TextBlock Text="PUERTO" Style="{StaticResource SectionHeaderTextStyle}" />
                                <TextBox Text="{x:Bind Editor.Port, Mode=TwoWay}" Width="120" />
                            </StackPanel>
                            <StackPanel Spacing="4">
                                <TextBlock Text="BAUD RATE" Style="{StaticResource SectionHeaderTextStyle}" />
                                <TextBox Text="{x:Bind Editor.BaudRate, Mode=TwoWay}" Width="120" />
                            </StackPanel>
                        </StackPanel>

                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" />
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="Spots" Style="{StaticResource SectionHeaderTextStyle}"
                                       VerticalAlignment="Center" />
                            <Button Grid.Column="1" Content="+ Spot"
                                    Command="{x:Bind Editor.AddSpotCommand}" />
                        </Grid>
                        <ItemsControl ItemsSource="{x:Bind Editor.Spots, Mode=OneWay}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="vm:SpotMappingRowVm">
                                    <StackPanel Orientation="Horizontal" Spacing="6" Margin="0,2">
                                        <TextBox Text="{x:Bind SpotId, Mode=TwoWay}" PlaceholderText="SpotId" Width="80" />
                                        <TextBox Text="{x:Bind SensorId, Mode=TwoWay}" PlaceholderText="SensorId" Width="80" />
                                        <TextBox Text="{x:Bind ActuatorId, Mode=TwoWay}" PlaceholderText="Actuador" Width="80" />
                                        <TextBox Text="{x:Bind Address, Mode=TwoWay}" PlaceholderText="Dirección" Width="120" />
                                        <TextBox Text="{x:Bind Type, Mode=TwoWay}" PlaceholderText="Tipo" Width="90" />
                                        <TextBox Text="{x:Bind Floor, Mode=TwoWay}" PlaceholderText="Planta" Width="90" />
                                        <Button Content="✕" Command="{x:Bind RemoveCommand}"
                                                AutomationProperties.Name="Eliminar spot" />
                                    </StackPanel>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>

                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" />
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="Gates" Style="{StaticResource SectionHeaderTextStyle}"
                                       VerticalAlignment="Center" />
                            <Button Grid.Column="1" Content="+ Gate"
                                    Command="{x:Bind Editor.AddGateCommand}" />
                        </Grid>
                        <ItemsControl ItemsSource="{x:Bind Editor.Gates, Mode=OneWay}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="vm:GateMappingRowVm">
                                    <StackPanel Orientation="Horizontal" Spacing="6" Margin="0,2">
                                        <TextBox Text="{x:Bind GateId, Mode=TwoWay}" PlaceholderText="GateId" Width="80" />
                                        <TextBox Text="{x:Bind Type, Mode=TwoWay}" PlaceholderText="ENTRY/EXIT" Width="90" />
                                        <TextBox Text="{x:Bind IrSensorId, Mode=TwoWay}" PlaceholderText="IR Sensor" Width="100" />
                                        <TextBox Text="{x:Bind ActuatorId, Mode=TwoWay}" PlaceholderText="Actuador" Width="80" />
                                        <TextBox Text="{x:Bind Pin, Mode=TwoWay}" PlaceholderText="Pin" Width="60" />
                                        <Button Content="✕" Command="{x:Bind RemoveCommand}"
                                                AutomationProperties.Name="Eliminar gate" />
                                    </StackPanel>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>

                        <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                            <Button Content="Guardar configuración"
                                    Command="{x:Bind Editor.SaveCommand}"
                                    Style="{StaticResource AccentButtonStyle}" />
                            <TextBlock Text="{x:Bind Editor.StatusMessage, Mode=OneWay}"
                                       Foreground="{ThemeResource Tx3Brush}"
                                       VerticalAlignment="Center" TextWrapping="Wrap" />
                        </StackPanel>
                    </StackPanel>
                </Border>
```

Nota: el namespace `vm` ya está declarado en la raíz del Page (`xmlns:vm="using:SmartParkingLot.Gui.ViewModels"`).

- [ ] **Step 3: Verificar compilación**

Run: `dotnet build src/GUI/SmartParkingLot.Gui.csproj -c Debug`
Expected: `Compilación correcta`.

- [ ] **Step 4: Verificación manual**

Run: `dotnet run --project src/GUI/SmartParkingLot.Gui.csproj -c Debug`
Pasos:
1. Ir a Hardware/Arduino → sección "Configuración".
2. Cambiar el baudRate a un valor no numérico → Guardar → debe mostrar "BaudRate inválido".
3. Duplicar un SpotId → Guardar → debe mostrar el error de duplicado y NO escribir.
4. Añadir un spot válido (ej. SpotId `C-03`, SensorId `IR10`, Actuador `LED10`) → Guardar → mensaje "Configuración guardada. Reinicia...".
5. Cerrar y reiniciar la app → el nuevo spot aparece en el Mapa de Spots (10 spots).

- [ ] **Step 5: Commit**

```bash
git add src/GUI/Pages/HardwarePage.xaml src/GUI/Pages/HardwarePage.xaml.cs src/GUI/Bootstrap/ServiceCollectionExtensions.cs
git commit -m "feat(gui): editor de hardware.json en la página Hardware/Arduino"
```

---

## Task 8: Verificación integral

**Files:** ninguno (solo verificación)

- [ ] **Step 1: Build de la solución completa**

Run: `dotnet build smart-parking-lot.sln -c Debug`
Expected: `0 Errores`.

- [ ] **Step 2: Ejecutar toda la batería de tests**

Run: `dotnet test smart-parking-lot.sln`
Expected: todos los tests pasan, incluyendo `HardwareConfigSaveTests` y `HardwareConfigValidatorTests`.

- [ ] **Step 3: Verificar el flujo CLI con el config consolidado**

Run: `dotnet run --project src/Cli/SmartParkingLot.Cli.csproj`
Expected: arranca leyendo el `hardware.json` canónico (9 spots) sin errores de carga.

---

## Notas y decisiones asumidas

- **Set canónico de spots:** se eligió el set de 9 de la GUI (más rico, coincide con la BD/demo y con las capturas) en lugar de los 4 del CLI. El CLI pasará a tener 9 spots.
- **Persistencia:** la GUI escribe en `AppContext.BaseDirectory/hardware.json` (el archivo que realmente carga). En desarrollo con `dotnet run`, una edición posterior del archivo canónico `config/hardware.json` (fuente más nueva) sobrescribiría la copia de salida por `PreserveNewest`; en un despliegue real (exe + json al lado) no aplica. Si se quiere persistencia robusta a prueba de rebuilds, la Fase 1.5 escribiría en `%LOCALAPPDATA%/SmartParkingLot/hardware.json` con precedencia en el loader.
- **Hot-reload fuera de alcance:** los cambios aplican al reiniciar (Fase 2/3 cubrirían aplicar en caliente puerto/baudRate y alta/baja de spots/gates).
- **Validación de tipos de spot:** la Fase 1 acepta texto libre en `Type`; un combo con valores fijos (Estándar/PMR/Moto) puede añadirse luego.
