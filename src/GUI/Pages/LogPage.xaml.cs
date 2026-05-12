using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Controls;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class LogPage : Page
{
    private readonly ParkingServices _svc;

    public LogPage(ParkingServices svc)
    {
        InitializeComponent();
        _svc = svc;
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QueryBox == null) return;
        QueryBox.PlaceholderText = TypeCombo.SelectedIndex switch
        {
            0 => "placa (ej: ABC-123)",
            1 => "sensorId (ej: SEN-SPOT-A-01)",
            2 => "deviceId (ej: GATE-G-01)",
            _ => ""
        };
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => _ = LoadAsync();

    private async void OnLoadClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        var key = (QueryBox.Text ?? "").Trim();
        ResultsList.Items.Clear();

        switch (TypeCombo.SelectedIndex)
        {
            case 0: // requests
                if (string.IsNullOrEmpty(key))
                {
                    HeaderSubtitle.Text = "Ingrese una placa para ver su historial.";
                    return;
                }
                var hist = (await _svc.Repository.GetRequestHistoryAsync(key)).ToList();
                HeaderSubtitle.Text = $"{hist.Count} solicitud(es) para placa {key}";
                foreach (var r in hist)
                {
                    ResultsList.Items.Add(BuildRow(
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        r.RequestType,
                        r.RequestType == "ENTRY" ? BadgeKind.Success : BadgeKind.Danger,
                        r.Approved ? "✓ APROBADO" : "✗ DENEGADO",
                        r.RequestId));
                }
                break;

            case 1: // sensor readings
                if (string.IsNullOrEmpty(key))
                {
                    HeaderSubtitle.Text = "Ingrese un sensorId.";
                    return;
                }
                var readings = (await _svc.Repository.GetSensorReadingsAsync(key)).ToList();
                HeaderSubtitle.Text = $"{readings.Count} lectura(s) para {key}";
                foreach (var r in readings)
                    ResultsList.Items.Add(BuildRow(
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        "SENSOR",
                        BadgeKind.Neutral,
                        $"valor: {r.Value}",
                        r.Id));
                break;

            case 2: // device actions
                if (string.IsNullOrEmpty(key))
                {
                    HeaderSubtitle.Text = "Ingrese un deviceId (ej: GATE-G-01).";
                    return;
                }
                var actions = (await _svc.Repository.GetDeviceActionsAsync(key)).ToList();
                HeaderSubtitle.Text = $"{actions.Count} acción(es) para {key}";
                foreach (var a in actions)
                    ResultsList.Items.Add(BuildRow(
                        a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        "DEVICE",
                        BadgeKind.Accent,
                        a.Action,
                        a.Id));
                break;
        }

        if (ResultsList.Items.Count == 0)
        {
            ResultsList.Items.Add(new TextBlock
            {
                Text = "Sin resultados.",
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"]
            });
        }
    }

    private static Grid BuildRow(string ts, string type, BadgeKind kind, string detail, string reference)
    {
        var grid = new Grid { Padding = new Thickness(14, 8, 14, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.BorderBrush = (Brush)XamlApp.Current.Resources["Stroke2Brush"];
        grid.BorderThickness = new Thickness(0, 0, 0, 1);

        var tsText = new TextBlock
        {
            Text = ts,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 11,
            Foreground = (Brush)XamlApp.Current.Resources["Tx2Brush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(tsText, 0);
        grid.Children.Add(tsText);

        var typeBadge = Badge.New(type, kind);
        Grid.SetColumn(typeBadge, 1);
        var badgeHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        badgeHost.Children.Add(typeBadge);
        Grid.SetColumn(badgeHost, 1);
        grid.Children.Add(badgeHost);

        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"],
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(detailText, 2);
        grid.Children.Add(detailText);

        var refText = new TextBlock
        {
            Text = reference,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 11,
            Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(refText, 3);
        grid.Children.Add(refText);

        return grid;
    }
}
