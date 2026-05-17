# MVVM Page Template — Smart Parking Lot GUI

## Patron a seguir para nuevas paginas (Fase 4+)

Cada pagina que se refactorice debe seguir este patron exacto.
El agente F4 debe replicar este esqueleto, ajustando solo los servicios especificos de cada pagina.

---

## Dependencias del ViewModel

El constructor siempre recibe:
1. `ILotSnapshotStream` — fuente reactiva de datos del lot
2. `IUiThreadDispatcher` — despacho seguro al hilo UI
3. Servicios especificos de la pagina (ej. `GuiLogger`, `IParkingRepository`, etc.)

---

## Esqueleto de ViewModel

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Gui.Bootstrap;   // GuiLogger
using SmartParkingLot.Gui.Infrastructure;

namespace SmartParkingLot.Gui.ViewModels;

public partial class XxxPageViewModel : ObservableObject
{
    private readonly ILotSnapshotStream _stream;
    private readonly IUiThreadDispatcher _ui;
    // + servicios especificos

    // Escalares enlazables
    [ObservableProperty] private int _someCount;
    [ObservableProperty] private string _someLabel = "";

    // Listas enlazables
    public ObservableCollection<SomeRowVm> Items { get; } = new();

    public XxxPageViewModel(
        ILotSnapshotStream stream,
        IUiThreadDispatcher ui
        /* , servicios especificos */)
    {
        _stream = stream;
        _ui = ui;
    }

    // Llamado desde OnNavigatedTo
    public void Activate()
    {
        _stream.SnapshotChanged += OnSnapshotChanged;
        ApplySnapshot(_stream.Current);
    }

    // Llamado desde OnNavigatedFrom
    public void Deactivate()
    {
        _stream.SnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(object? sender, LotSnapshotDto dto)
    {
        _ui.Enqueue(() => ApplySnapshot(dto));
    }

    private void ApplySnapshot(LotSnapshotDto dto)
    {
        // Actualizar propiedades y colecciones
        Items.Clear();
        foreach (var item in dto.ZoneSummaries)
            Items.Add(new SomeRowVm { /* ... */ });
    }

    [RelayCommand]
    private void Refresh()
    {
        ApplySnapshot(_stream.Current);
    }
}
```

---

## Esqueleto de Page (code-behind)

```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartParkingLot.Gui.ViewModels;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class XxxPage : Page
{
    public XxxPageViewModel ViewModel { get; }

    public XxxPage(XxxPageViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Activate();
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Deactivate();
}
```

---

## Esqueleto de XAML

```xml
<Page x:Class="SmartParkingLot.Gui.Pages.XxxPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:vm="using:SmartParkingLot.Gui.ViewModels">

    <!-- Bindings: {x:Bind ViewModel.Propiedad, Mode=OneWay} -->
    <!-- Comandos:  Command="{x:Bind ViewModel.AlgoCommand}" -->

    <!-- Listas: ItemsRepeater con DataTemplate x:DataType="vm:SomeRowVm" -->
    <ItemsRepeater ItemsSource="{x:Bind ViewModel.Items, Mode=OneWay}">
        <ItemsRepeater.Layout>
            <StackLayout Spacing="8" Orientation="Vertical" />
        </ItemsRepeater.Layout>
        <ItemsRepeater.ItemTemplate>
            <DataTemplate x:DataType="vm:SomeRowVm">
                <TextBlock Text="{x:Bind Label}" />
            </DataTemplate>
        </ItemsRepeater.ItemTemplate>
    </ItemsRepeater>
</Page>
```

---

## Registro en ServiceCollectionExtensions.AddGuiViewModels()

```csharp
services.AddTransient<XxxPageViewModel>(sp =>
    new XxxPageViewModel(
        sp.GetRequiredService<ILotSnapshotStream>(),
        sp.GetRequiredService<IUiThreadDispatcher>()
        /* , sp.GetRequiredService<ServicioEspecifico>() */));

services.AddTransient<Pages.XxxPage>(sp =>
    new Pages.XxxPage(sp.GetRequiredService<XxxPageViewModel>()));
```

---

## ViewModels de fila (Row VMs)

Clases simples (no `ObservableObject`) con propiedades `init` y computed:

```csharp
namespace SmartParkingLot.Gui.ViewModels;

public class SomeRowVm
{
    public string Label { get; init; } = "";
    public int Value { get; init; }
    public string Display => $"{Value} unidades";
}
```

---

## Reglas de aceptacion

- Cero usos de `new Grid()`, `new Border()`, `new StackPanel()`, `new TextBlock()` en code-behind
- Cero usos de `TryEnqueue`, `DispatcherQueue`, `_svc`
- `OnNavigatedTo` y `OnNavigatedFrom` son los unicos metodos de ciclo de vida en la Page
- Todas las listas usan `ObservableCollection<T>` con `ItemsRepeater` en XAML
- Todos los escalares usan `[ObservableProperty]` con `{x:Bind ..., Mode=OneWay}` en XAML
