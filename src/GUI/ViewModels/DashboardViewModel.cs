using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Application.Queries;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Infrastructure;

namespace SmartParkingLot.Gui.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ILotSnapshotStream _stream;
    private readonly IUiThreadDispatcher _ui;
    private readonly GuiLogger _logger;

    [ObservableProperty] private int _occupiedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _availableCount;
    [ObservableProperty] private int _occupancyPct;
    [ObservableProperty] private string _lastUpdated = "";
    [ObservableProperty] private bool _hasAlerts;
    [ObservableProperty] private string _alertBadgeText = "Sin alertas";

    public ObservableCollection<ZoneRowVm> Zones { get; } = new();
    public ObservableCollection<AlertRowVm> Alerts { get; } = new();
    public ObservableCollection<GateRowVm> Gates { get; } = new();

    public DashboardViewModel(
        ILotSnapshotStream stream,
        IUiThreadDispatcher ui,
        GuiLogger logger)
    {
        _stream = stream;
        _ui     = ui;
        _logger = logger;
    }

    public void Activate()
    {
        _stream.SnapshotChanged += OnSnapshotChanged;
        ApplySnapshot(_stream.Current);
        RefreshAlerts();
    }

    public void Deactivate()
    {
        _stream.SnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(object? sender, LotSnapshotDto dto)
    {
        _ui.Enqueue(() =>
        {
            ApplySnapshot(dto);
            RefreshAlerts();
        });
    }

    private void ApplySnapshot(LotSnapshotDto dto)
    {
        TotalCount     = dto.TotalSpots;
        OccupiedCount  = dto.OccupiedSpots;
        AvailableCount = dto.TotalSpots - dto.OccupiedSpots;
        OccupancyPct   = dto.TotalSpots == 0
            ? 0 : (int)Math.Round(100.0 * dto.OccupiedSpots / dto.TotalSpots);
        LastUpdated    = $"Última actualización: {DateTime.Now:HH:mm:ss}";

        Zones.Clear();
        foreach (var z in dto.ZoneSummaries)
            Zones.Add(new ZoneRowVm { Zone = z.Zone, Occupied = z.Occupied, Total = z.Total });

        Gates.Clear();
        foreach (var g in dto.Gates)
            Gates.Add(new GateRowVm { GateId = g.GateId, Type = g.Type, IsOpen = g.IsOpen });
    }

    private void RefreshAlerts()
    {
        var entries = _logger.Snapshot()
            .Where(e => e.Level is LogLevel.Warning or LogLevel.Error)
            .Reverse()
            .Take(5)
            .ToList();

        Alerts.Clear();
        foreach (var e in entries)
            Alerts.Add(new AlertRowVm
            {
                Message = e.Message,
                Source  = e.Source,
                Time    = e.Timestamp.ToString("HH:mm:ss"),
                IsError = e.Level == LogLevel.Error
            });

        HasAlerts      = Alerts.Count > 0;
        AlertBadgeText = Alerts.Count > 0 ? $"{Alerts.Count} activas" : "Sin alertas";
    }

    [RelayCommand]
    private void Refresh()
    {
        ApplySnapshot(_stream.Current);
        RefreshAlerts();
    }
}
