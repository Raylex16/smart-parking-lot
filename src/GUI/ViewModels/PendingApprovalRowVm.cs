using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SmartParkingLot.Application.Approvals;
using SmartParkingLot.Core.Approvals;

namespace SmartParkingLot.Gui.ViewModels;

public partial class PendingApprovalRowVm : ObservableObject, IDisposable
{
    private readonly DispatcherQueueTimer _timer;

    public string Id     { get; }
    public string Plate  { get; }
    public string GateId { get; }

    [ObservableProperty] private int  _elapsedSeconds;
    [ObservableProperty] private bool _isResolved;

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
