using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Gui.ViewModels;

public partial class LogPageViewModel : ObservableObject
{
    private readonly IParkingRepository _repository;

    [ObservableProperty] private string _headerSubtitle = "Selecciona un tipo e ingresa un filtro.";
    [ObservableProperty] private string _queryText = "";
    [ObservableProperty] private int _selectedTypeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    private bool _hasNoResults;

    public Visibility EmptyVisibility => HasNoResults ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<LogRowVm> Results { get; } = new();

    public LogPageViewModel(IParkingRepository repository)
    {
        _repository = repository;
    }

    public void Activate() { }
    public void Deactivate() { }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var key = QueryText.Trim();
        Results.Clear();

        switch (SelectedTypeIndex)
        {
            case 0: // requests
                if (string.IsNullOrEmpty(key))
                {
                    HeaderSubtitle = "Ingrese una placa para ver su historial.";
                    break;
                }
                var hist = (await _repository.GetRequestHistoryAsync(key)).ToList();
                HeaderSubtitle = $"{hist.Count} solicitud(es) para placa {key}";
                foreach (var r in hist)
                    Results.Add(new LogRowVm
                    {
                        Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        TypeLabel = r.RequestType,
                        BadgeKind = r.RequestType == "ENTRY" ? "Success" : "Danger",
                        Detail    = r.Approved ? "✓ APROBADO" : "✗ DENEGADO",
                        Reference = r.RequestId
                    });
                break;

            case 1: // sensor readings
                if (string.IsNullOrEmpty(key))
                {
                    HeaderSubtitle = "Ingrese un sensorId.";
                    break;
                }
                var readings = (await _repository.GetSensorReadingsAsync(key)).ToList();
                HeaderSubtitle = $"{readings.Count} lectura(s) para {key}";
                foreach (var r in readings)
                    Results.Add(new LogRowVm
                    {
                        Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        TypeLabel = "SENSOR",
                        BadgeKind = "Neutral",
                        Detail    = $"valor: {r.Value}",
                        Reference = r.Id
                    });
                break;

            case 2: // device actions
                if (string.IsNullOrEmpty(key))
                {
                    HeaderSubtitle = "Ingrese un deviceId (ej: GATE-G-01).";
                    break;
                }
                var actions = (await _repository.GetDeviceActionsAsync(key)).ToList();
                HeaderSubtitle = $"{actions.Count} acción(es) para {key}";
                foreach (var a in actions)
                    Results.Add(new LogRowVm
                    {
                        Timestamp = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        TypeLabel = "DEVICE",
                        BadgeKind = "Accent",
                        Detail    = a.Action,
                        Reference = a.Id
                    });
                break;
        }

        HasNoResults = Results.Count == 0;
    }
}
