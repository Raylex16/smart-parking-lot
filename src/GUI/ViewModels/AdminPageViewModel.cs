using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Gui.ViewModels;

public partial class AdminPageViewModel : ObservableObject
{
    private readonly IParkingRepository _repository;
    private readonly ParkingLot _lot;
    private List<SpotAdminRowVm> _allSpots = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _typeFilter = "";
    [ObservableProperty] private string _footerText = "";

    public ObservableCollection<SpotAdminRowVm> FilteredSpots { get; } = new();

    public AdminPageViewModel(IParkingRepository repository, ParkingLot lot)
    {
        _repository = repository;
        _lot = lot;
    }

    public void Activate() => _ = ReloadAsync();
    public void Deactivate() { }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnTypeFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task ReloadAsync()
    {
        var spots = await _repository.GetSpotsByLotIdAsync(_lot.Id);
        _allSpots = spots.Select(s => new SpotAdminRowVm
        {
            Id         = s.Id,
            Address    = s.Address,
            Type       = s.Type,
            Floor      = s.Floor,
            IsOccupied = s.IsOccupied
        }).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim().ToLowerInvariant();
        var filtered = _allSpots.Where(s =>
        {
            if (!string.IsNullOrEmpty(TypeFilter) &&
                !string.Equals(s.Type, TypeFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (search.Length == 0) return true;
            return s.Id.ToLowerInvariant().Contains(search)
                || s.Type.ToLowerInvariant().Contains(search)
                || s.Address.ToLowerInvariant().Contains(search);
        }).ToList();

        FilteredSpots.Clear();
        foreach (var s in filtered)
            FilteredSpots.Add(s);

        FooterText = $"Mostrando {filtered.Count} de {_allSpots.Count} spots";
    }
}
