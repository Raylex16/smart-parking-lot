using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Controls;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class MapPage : Page
{
    private readonly ParkingServices _svc;
    private readonly DispatcherQueue _ui;
    private readonly Dictionary<string, Border> _spotTiles = new();

    public MapPage(ParkingServices svc)
    {
        InitializeComponent();
        _svc = svc;
        _ui = DispatcherQueue.GetForCurrentThread();
        Loaded += (_, _) => Build();

        foreach (var spot in _svc.Lot.GetSpots())
            spot.OccupancyChanged += OnSpotChanged;
    }

    private void OnSpotChanged(SpotOccupancyChanged evt)
    {
        _ui.TryEnqueue(() =>
        {
            if (_spotTiles.TryGetValue(evt.SpotId, out var tile))
                StyleSpot(tile, evt.IsOccupied);
            UpdateSummary();
            UpdateMapSubtitle();
        });
    }

    private void Build()
    {
        BuildMap();
        BuildGatePanel();
        UpdateSummary();
        UpdateMapSubtitle();
        MapTitle.Text = $"Planta 1 — {_svc.Lot.Name}";
    }

    private void BuildMap()
    {
        ZonesPanel.Children.Clear();
        _spotTiles.Clear();

        var spots = _svc.Lot.GetSpots();
        var zones = spots.GroupBy(s => ZoneOf(s.Id))
                         .OrderBy(g => g.Key)
                         .ToList();

        foreach (var zone in zones)
        {
            // Zone header
            var hdr = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            hdr.Children.Add(new TextBlock
            {
                Text = $"ZONA {zone.Key}",
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                CharacterSpacing = 80,
                Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"]
            });
            var line = new Border
            {
                Height = 1,
                Background = (Brush)XamlApp.Current.Resources["Stroke2Brush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(line, 1);
            hdr.Children.Add(line);

            var grid = new VariableSizedWrapGrid
            {
                Orientation = Orientation.Horizontal,
                ItemHeight = 60,
                ItemWidth = 48,
            };
            foreach (var spot in zone)
            {
                var tile = CreateSpotTile(spot);
                _spotTiles[spot.Id] = tile;
                grid.Children.Add(tile);
            }

            var zonePanel = new StackPanel();
            zonePanel.Children.Add(hdr);
            zonePanel.Children.Add(grid);

            ZonesPanel.Children.Add(zonePanel);
        }
    }

    private Border CreateSpotTile(ParkingSpot spot)
    {
        var tile = new Border
        {
            Width = 44,
            Height = 54,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            Tag = spot.Id
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2
        };
        stack.Children.Add(new TextBlock
        {
            Text = spot.Id,
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"]
        });
        stack.Children.Add(new TextBlock
        {
            Text = ShortType(spot.Type),
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"]
        });
        tile.Child = stack;
        StyleSpot(tile, spot.IsOccupied);

        ToolTipService.SetToolTip(tile, $"{spot.Id} — {spot.Address} ({spot.Type})");
        tile.PointerPressed += (s, e) => ToggleSpot(spot);
        return tile;
    }

    private static void StyleSpot(Border tile, bool occupied)
    {
        var res = XamlApp.Current.Resources;
        if (occupied)
        {
            tile.Background = (Brush)res["AccentLightBrush"];
            tile.BorderBrush = (Brush)res["AccentFillColorDefaultBrush"];
        }
        else
        {
            tile.Background = (Brush)res["Layer2Brush"];
            tile.BorderBrush = (Brush)res["StrokeBrush"];
        }
    }

    private static string ShortType(string type) => type.ToLowerInvariant() switch
    {
        var t when t.Contains("est") => "STD",
        var t when t.Contains("pmr") || t.Contains("disc") => "PMR",
        var t when t.Contains("moto") => "MOTO",
        _ => type.Length > 4 ? type[..4].ToUpper() : type.ToUpper()
    };

    private void ToggleSpot(ParkingSpot spot)
    {
        var newState = !spot.IsOccupied;
        if (!_svc.SpotSensors.TryGetValue(spot.Id, out var sensor)) return;

        var reading = new SpotSensorReading(spot.Id, newState);
        sensor.CaptureReading(reading);

        var raw = newState ? "1" : "0";
        _ = _svc.Repository.LogSensorReadingAsync(sensor.Id, raw, DateTime.Now);
        _svc.Bus.Publish(new SensorReadingReceived(
            SensorId: sensor.Id,
            SensorType: sensor.GetSensorType(),
            RawValue: raw,
            Timestamp: DateTimeOffset.Now));
    }

    private void BuildGatePanel()
    {
        GateControlPanel.Children.Clear();
        foreach (var cfg in _svc.Config.Gates)
        {
            var gate = _svc.GateController.GetGateById(cfg.GateId);
            var open = gate?.GetState() ?? false;

            var block = new StackPanel { Spacing = 4 };

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.Children.Add(new TextBlock
            {
                Text = cfg.GateId,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"]
            });
            var stateBadge = Badge.New(open ? "Abierta" : "Cerrada",
                open ? BadgeKind.Success : BadgeKind.Neutral);
            Grid.SetColumn(stateBadge, 1);
            topRow.Children.Add(stateBadge);
            block.Children.Add(topRow);

            block.Children.Add(Badge.New(cfg.Type.ToString(),
                cfg.Type == GateType.ENTRY ? BadgeKind.Accent : BadgeKind.Neutral));

            var openBtn = new Button
            {
                Content = open ? "Cerrar" : "Abrir",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            openBtn.Click += (_, _) =>
            {
                if (gate is null) return;
                if (open) gate.Close(); else gate.Open();
                _ = _svc.Repository.LogDeviceActionAsync($"GATE-{cfg.GateId}",
                    open ? "CLOSE" : "OPEN", DateTime.Now);
                BuildGatePanel();
            };
            block.Children.Add(openBtn);

            var separator = new Border
            {
                Height = 1,
                Background = (Brush)XamlApp.Current.Resources["Stroke2Brush"],
                Margin = new Thickness(0, 6, 0, 0)
            };
            block.Children.Add(separator);

            GateControlPanel.Children.Add(block);
        }

        BuildGateStrip(EntryGatesPanel, GateType.ENTRY);
        BuildGateStrip(ExitGatesPanel, GateType.EXIT);
    }

    private void BuildGateStrip(ItemsControl host, GateType type)
    {
        var items = new List<UIElement>();
        foreach (var cfg in _svc.Config.Gates.Where(g => g.Type == type))
        {
            var gate = _svc.GateController.GetGateById(cfg.GateId);
            var open = gate?.GetState() ?? false;
            var pill = new Border
            {
                Height = 24,
                Padding = new Thickness(10, 0, 10, 0),
                CornerRadius = new CornerRadius(12),
                Background = open
                    ? (Brush)XamlApp.Current.Resources["SuccessBackground"]
                    : (Brush)XamlApp.Current.Resources["Layer2Brush"],
                BorderBrush = open
                    ? (Brush)XamlApp.Current.Resources["SuccessBrush"]
                    : (Brush)XamlApp.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1)
            };
            var pillStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center
            };
            pillStack.Children.Add(new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "",
                FontSize = 10,
                Foreground = open
                    ? (Brush)XamlApp.Current.Resources["SuccessBrush"]
                    : (Brush)XamlApp.Current.Resources["Tx2Brush"]
            });
            pillStack.Children.Add(new TextBlock
            {
                Text = cfg.GateId,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = open
                    ? (Brush)XamlApp.Current.Resources["SuccessBrush"]
                    : (Brush)XamlApp.Current.Resources["Tx2Brush"]
            });
            pill.Child = pillStack;
            items.Add(pill);
        }
        host.ItemsSource = items;
    }

    private void UpdateSummary()
    {
        SummaryGrid.Children.Clear();
        SummaryGrid.RowDefinitions.Clear();

        var spots = _svc.Lot.GetSpots();
        var libres = spots.Count(s => !s.IsOccupied);
        var ocupados = spots.Count(s => s.IsOccupied);

        AddSummaryRow("Libres", libres.ToString(), BadgeKind.Success, 0);
        AddSummaryRow("Ocupados", ocupados.ToString(), BadgeKind.Accent, 1);
        AddSummaryRow("Total", spots.Count.ToString(), BadgeKind.Neutral, 2);
    }

    private void AddSummaryRow(string label, string value, BadgeKind kind, int row)
    {
        SummaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var rowGrid = new Grid { Padding = new Thickness(0, 5, 0, 5) };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rowGrid.BorderBrush = (Brush)XamlApp.Current.Resources["Stroke2Brush"];
        rowGrid.BorderThickness = new Thickness(0, 0, 0, 1);

        rowGrid.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = (Brush)XamlApp.Current.Resources["Tx2Brush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        var badge = Badge.New(value, kind);
        Grid.SetColumn(badge, 1);
        rowGrid.Children.Add(badge);

        Grid.SetRow(rowGrid, row);
        SummaryGrid.Children.Add(rowGrid);
    }

    private void UpdateMapSubtitle()
    {
        var spots = _svc.Lot.GetSpots();
        var occ = spots.Count(s => s.IsOccupied);
        MapSubtitle.Text = $"{occ}/{spots.Count} ocupados";
    }

    private static string ZoneOf(string spotId)
    {
        var dash = spotId.IndexOf('-');
        return dash > 0 ? spotId[..dash] : spotId[..1];
    }

    private async void OnEntryClick(object sender, RoutedEventArgs e)
    {
        var plate = await PromptForPlateAsync("Solicitar entrada", "Placa del vehículo:");
        if (string.IsNullOrWhiteSpace(plate)) return;

        var occupiedBefore = _svc.Lot.GetSpots()
            .Where(s => s.IsOccupied).Select(s => s.Id).ToHashSet();

        var gateReading = new GateSensorReading(plate, GuiConstants.ENTRY_GATE_ID);
        _svc.GateSensor.CaptureReading(gateReading);
        await _svc.Repository.LogSensorReadingAsync(_svc.GateSensor.Id, $"plate:{plate}", DateTime.Now);

        var request = new EntryRequest(plate) { GateId = GuiConstants.ENTRY_GATE_ID };
        await _svc.GateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _svc.Repository.LogRequestAsync(requestId, plate, "ENTRY", _svc.Lot.Id,
            request.Timestamp, request.Approved);

        if (request.Approved)
        {
            var assigned = _svc.Lot.GetSpots()
                .FirstOrDefault(s => s.IsOccupied && !occupiedBefore.Contains(s.Id));
            if (assigned is not null)
                await _svc.Repository.UpdateSpotStatusAsync(assigned.Id, true);
            await _svc.Repository.LogDeviceActionAsync($"GATE-{GuiConstants.ENTRY_GATE_ID}",
                "OPEN", DateTime.Now);
        }

        await ShowResultDialog(request.Approved
            ? $"✓ Entrada CONCEDIDA para {plate}.\nDisponibles: {_svc.Lot.AvailableSpots}"
            : $"✗ Entrada DENEGADA para {plate}.\nDisponibles: {_svc.Lot.AvailableSpots}");
    }

    private async void OnExitClick(object sender, RoutedEventArgs e)
    {
        var plate = await PromptForPlateAsync("Solicitar salida", "Placa del vehículo:");
        if (string.IsNullOrWhiteSpace(plate)) return;

        var request = new ExitRequest(plate) { GateId = GuiConstants.EXIT_GATE_ID };
        await _svc.GateController.HandleRequestAsync(request);

        var requestId = $"REQ-{Guid.NewGuid().ToString("N")[..8]}";
        await _svc.Repository.LogRequestAsync(requestId, plate, "EXIT", _svc.Lot.Id,
            request.Timestamp, approved: true);
        await _svc.Repository.LogDeviceActionAsync($"GATE-{GuiConstants.EXIT_GATE_ID}", "OPEN", DateTime.Now);

        await ShowResultDialog($"✓ Puerta de salida abierta para {plate}.\n" +
                               "La liberación del spot la detecta el sensor.");
    }

    private void OnSimEntryIrClick(object sender, RoutedEventArgs e) => SimulateIr("GATE-IR1");
    private void OnSimExitIrClick(object sender, RoutedEventArgs e) => SimulateIr("GATE-IR2");

    private void SimulateIr(string irSensorId)
    {
        _svc.Bus.Publish(new SensorReadingReceived(
            SensorId: irSensorId,
            SensorType: "IR",
            RawValue: "1",
            Timestamp: DateTimeOffset.Now));
    }

    private async Task<string> PromptForPlateAsync(string title, string label)
    {
        var input = new TextBox
        {
            PlaceholderText = "ABC-123",
            MinWidth = 240
        };
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "Aceptar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = label, Foreground = (Brush)XamlApp.Current.Resources["Tx2Brush"] },
                    input
                }
            }
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? input.Text.Trim() : string.Empty;
    }

    private async Task ShowResultDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Resultado",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Ok",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }
}
