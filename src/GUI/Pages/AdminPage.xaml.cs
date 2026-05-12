using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Core;
using SmartParkingLot.Gui.Bootstrap;
using SmartParkingLot.Gui.Controls;

namespace SmartParkingLot.Gui.Pages;

public sealed partial class AdminPage : Page
{
    private readonly ParkingServices _svc;
    private List<ParkingSpot> _all = new();

    public AdminPage(ParkingServices svc)
    {
        InitializeComponent();
        _svc = svc;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _all = (await _svc.Repository.GetSpotsByLotIdAsync(_svc.Lot.Id)).ToList();
        RenderRows();
    }

    private void RenderRows()
    {
        var typeFilter = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var search = (SearchBox.Text ?? "").Trim().ToLowerInvariant();

        var filtered = _all.Where(s =>
        {
            if (!string.IsNullOrEmpty(typeFilter) && !string.Equals(s.Type, typeFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (search.Length == 0) return true;
            return s.Id.ToLowerInvariant().Contains(search)
                || s.Type.ToLowerInvariant().Contains(search)
                || s.Address.ToLowerInvariant().Contains(search);
        }).ToList();

        RowsPanel.Children.Clear();
        foreach (var s in filtered)
            RowsPanel.Children.Add(BuildRow(s));

        FooterText.Text = $"Mostrando {filtered.Count} de {_all.Count} spots";
    }

    private static Grid BuildRow(ParkingSpot s)
    {
        var row = new Grid { Padding = new Thickness(14, 8, 14, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.BorderBrush = (Brush)XamlApp.Current.Resources["Stroke2Brush"];
        row.BorderThickness = new Thickness(0, 0, 0, 1);

        var idText = new TextBlock
        {
            Text = s.Id,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)XamlApp.Current.Resources["Tx1Brush"]
        };
        row.Children.Add(idText);

        var addressText = new TextBlock
        {
            Text = s.Address,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)XamlApp.Current.Resources["Tx2Brush"],
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(addressText, 1);
        row.Children.Add(addressText);

        var typeBadge = Badge.New(s.Type,
            s.Type.Equals("PMR", StringComparison.OrdinalIgnoreCase) ? BadgeKind.Warning : BadgeKind.Neutral);
        var typeHost = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        typeHost.Children.Add(typeBadge);
        Grid.SetColumn(typeHost, 2);
        row.Children.Add(typeHost);

        var floorText = new TextBlock
        {
            Text = s.Floor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)XamlApp.Current.Resources["Tx3Brush"]
        };
        Grid.SetColumn(floorText, 3);
        row.Children.Add(floorText);

        var stateBadge = Badge.New(s.IsOccupied ? "Ocupado" : "Libre",
            s.IsOccupied ? BadgeKind.Accent : BadgeKind.Success);
        var stateHost = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        stateHost.Children.Add(stateBadge);
        Grid.SetColumn(stateHost, 4);
        row.Children.Add(stateHost);

        return row;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            RenderRows();
    }

    private void OnTypeFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RowsPanel != null) RenderRows();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ReloadAsync();
}
