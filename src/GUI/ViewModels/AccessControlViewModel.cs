using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Application.Observability;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Entities;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Gui.Infrastructure;

namespace SmartParkingLot.Gui.ViewModels;

public partial class AccessControlViewModel : ObservableObject
{
    private readonly IParkingModeService        _modeService;
    private readonly IAccessPolicyConfigService _configService;
    private readonly ILotSnapshotStream         _stream;
    private readonly IUiThreadDispatcher        _ui;
    private readonly IApprovalQueue             _approvalQueue;
    private readonly IApprovalDecisionService   _approvalDecisions;
    private readonly DispatcherQueue            _dq;
    private readonly ParkingLot                 _lot;
    private Action<PendingApproval>?            _approvalHandler;

    [ObservableProperty] private int                _selectedModeIndex;
    [ObservableProperty] private string             _policyBadgeText  = "";
    [ObservableProperty] private SolidColorBrush    _policyBadgeColor = new(Colors.Green);
    [ObservableProperty] private bool               _showWhitelistPanel;
    [ObservableProperty] private bool               _showSchedulePanel;
    [ObservableProperty] private string             _newPlate         = "";
    [ObservableProperty] private TimeSpan           _scheduleStart    = TimeSpan.FromHours(8);
    [ObservableProperty] private TimeSpan           _scheduleEnd      = TimeSpan.FromHours(20);
    [ObservableProperty] private string             _statusMessage    = "";
    [ObservableProperty] private bool               _hasStatusMessage;
    [ObservableProperty] private bool               _whitelistIsEmpty;

    public ObservableCollection<PlateRowVm>           Plates           { get; } = new();
    public ObservableCollection<PendingApprovalRowVm> PendingApprovals { get; } = new();

    [ObservableProperty] private string _approvalsBadgeText = "Sin pendientes";

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
        IUiThreadDispatcher ui,
        IApprovalQueue approvalQueue,
        IApprovalDecisionService approvalDecisions,
        DispatcherQueue dq,
        ParkingLot lot)
    {
        _modeService       = modeService;
        _configService     = configService;
        _stream            = stream;
        _ui                = ui;
        _approvalQueue     = approvalQueue;
        _approvalDecisions = approvalDecisions;
        _dq                = dq;
        _lot               = lot;
    }

    public void Activate()
    {
        _ = LoadAsync();

        foreach (var a in _approvalQueue.GetPending())
            AddApprovalRow(a);

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

    private async Task LoadAsync()
    {
        var lotId  = _lot.Id;
        var config = await _configService.GetAsync(lotId);

        SelectedModeIndex = Array.IndexOf(ModeIndexMap, _modeService.Current);
        UpdatePanelVisibility(_modeService.Current);
        UpdateBadge(_modeService.Current);

        Plates.Clear();
        foreach (var p in config.AllowedPlates)
            Plates.Add(BuildPlateRow(p));

        ScheduleStart    = config.ScheduleStart;
        ScheduleEnd      = config.ScheduleEnd;
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
        NewPlate         = "";
        WhitelistIsEmpty = false;
    }

    [RelayCommand]
    private async Task SaveWhitelistAsync()
    {
        var lotId  = _lot.Id;
        var plates = Plates.Select(r => r.Plate).ToList();
        await _configService.UpdateWhitelistAsync(lotId, plates);
        WhitelistIsEmpty = plates.Count == 0;
        StatusMessage    = $"Whitelist guardada ({plates.Count} placa(s))";
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (ScheduleEnd <= ScheduleStart)
        {
            StatusMessage = "Error: la hora de fin debe ser posterior a la de inicio.";
            return;
        }
        var lotId = _lot.Id;
        await _configService.UpdateScheduleAsync(lotId, ScheduleStart, ScheduleEnd);
        StatusMessage = $"Horario guardado ({ScheduleStart:hh\\:mm}–{ScheduleEnd:hh\\:mm})";
    }

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

    partial void OnStatusMessageChanged(string value)
        => HasStatusMessage = !string.IsNullOrEmpty(value);

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
