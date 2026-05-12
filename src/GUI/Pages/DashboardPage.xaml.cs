using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Core;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Controls;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly ParkingServices _svc;
    private readonly DispatcherQueue _ui;

    public DashboardPage(ParkingServices svc)
    {
        InitializeComponent();
        _svc = svc;
        _ui = DispatcherQueue.GetForCurrentThread();
        Loaded += (_, _) => Refresh();

        foreach (var spot in _svc.Lot.GetSpots())
            spot.OccupancyChanged += _ => _ui.TryEnqueue(Refresh);
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        // Pull live data from DB
        await _svc.Repository.GetSpotsByLotIdAsync(_svc.Lot.Id);
        Refresh();
    }

    private void Refresh()
    {
        var spots = _svc.Lot.GetSpots();
        var occ = spots.Count(s => s.IsOccupied);
        var tot = spots.Count;
        var avail = tot - occ;
        var pct = tot == 0 ? 0 : (int)Math.Round(100.0 * occ / tot);

        TileOccupied.Text = occ.ToString();
        TileOccupiedSub.Text = $"de {tot} totales";
        TileAvailable.Text = avail.ToString();
        TileAvailableSub.Text = "spots libres";
        TilePct.Text = $"{pct}%";
        TileRequests.Text = "—";
        TileRequestsSub.Text = "ver Historial";
        UpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";

        BuildZones(spots);
        BuildAlerts();
        BuildGates();
    }

    private void BuildZones(IReadOnlyList<ParkingSpot> spots)
    {
        ZonesPanel.Children.Clear();
        var zones = spots.GroupBy(s => ZoneOf(s.Id)).OrderBy(g => g.Key);
        foreach (var z in zones)
        {
            var zo = z.Count(s => s.IsOccupied);
            var zt = z.Count();
            var zp = zt == 0 ? 0 : (int)Math.Round(100.0 * zo / zt);

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock
            {
                Text = $"Zona {z.Key}",
                FontSize = 12,
                Foreground = (Brush)XamlApp.Current.Resources["Tx2Brush"]
            });
            var right = new TextBlock
            {
                Text = $"{zo}/{zt}",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"]
            };
            Grid.SetColumn(right, 1);
            header.Children.Add(right);

            var bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = zp,
                Height = 4,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = ResolvePctBrush(zp)
            };

            var stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(bar);
            ZonesPanel.Children.Add(stack);
        }
    }

    private void BuildAlerts()
    {
        AlertsPanel.Children.Clear();

        // Pull recent log entries flagged as warnings/errors
        var entries = _svc.UiLogger.Snapshot()
            .Where(e => e.Level is Core.Interfaces.LogLevel.Warning or Core.Interfaces.LogLevel.Error)
            .Reverse()
            .Take(5)
            .ToList();

        if (entries.Count == 0)
        {
            AlertsBadgeHost.Content = Badge.New("Sin alertas", BadgeKind.Success);
            AlertsPanel.Children.Add(new TextBlock
            {
                Text = "Todo en orden.",
                FontSize = 12,
                Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"],
                Margin = new Thickness(0, 4, 0, 0)
            });
            return;
        }

        AlertsBadgeHost.Content = Badge.New($"{entries.Count} activas", BadgeKind.Warning);

        foreach (var e in entries)
        {
            var kind = e.Level == Core.Interfaces.LogLevel.Error ? BadgeKind.Danger : BadgeKind.Warning;
            var row = new Grid { Padding = new Thickness(0, 7, 0, 7) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.BorderBrush = (Brush)XamlApp.Current.Resources["Stroke2Brush"];
            row.BorderThickness = new Thickness(0, 0, 0, 1);

            var icon = new FontIcon
            {
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                Glyph = e.Level == Core.Interfaces.LogLevel.Error ? "" : "",
                FontSize = 14,
                Foreground = kind == BadgeKind.Danger
                    ? (Brush)XamlApp.Current.Resources["DangerBrush"]
                    : (Brush)XamlApp.Current.Resources["WarningBrush"],
                Margin = new Thickness(0, 0, 10, 0)
            };
            row.Children.Add(icon);

            var col = new StackPanel();
            Grid.SetColumn(col, 1);
            col.Children.Add(new TextBlock
            {
                Text = e.Message,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            col.Children.Add(new TextBlock
            {
                Text = $"{e.Source} · {e.Timestamp:HH:mm:ss}",
                FontSize = 10,
                Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"]
            });
            row.Children.Add(col);

            AlertsPanel.Children.Add(row);
        }
    }

    private void BuildGates()
    {
        GatesPanel.Children.Clear();
        foreach (var gateCfg in _svc.Config.Gates)
        {
            var gate = _svc.GateController.GetGateById(gateCfg.GateId);
            var open = gate?.GetState() ?? false;

            var card = new Border
            {
                Background = (Brush)XamlApp.Current.Resources["Layer2Brush"],
                BorderBrush = (Brush)XamlApp.Current.Resources["Stroke2Brush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Width = 200
            };
            var stack = new StackPanel { Spacing = 6 };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(new TextBlock
            {
                Text = gateCfg.GateId,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"]
            });
            var stateBadge = Badge.New(open ? "Abierta" : "Cerrada",
                open ? BadgeKind.Success : BadgeKind.Neutral);
            Grid.SetColumn(stateBadge, 1);
            top.Children.Add(stateBadge);
            stack.Children.Add(top);

            var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            pills.Children.Add(Badge.New(gateCfg.Type.ToString(),
                gateCfg.Type == GateType.ENTRY ? BadgeKind.Accent : BadgeKind.Neutral));
            stack.Children.Add(pills);

            card.Child = stack;
            GatesPanel.Children.Add(card);
        }
    }

    private static string ZoneOf(string spotId)
    {
        var dash = spotId.IndexOf('-');
        return dash > 0 ? spotId[..dash] : spotId[..1];
    }

    private static Brush ResolvePctBrush(int pct)
    {
        var res = XamlApp.Current.Resources;
        if (pct >= 90) return (Brush)res["DangerBrush"];
        if (pct >= 70) return (Brush)res["WarningBrush"];
        return (Brush)res["SuccessBrush"];
    }
}
