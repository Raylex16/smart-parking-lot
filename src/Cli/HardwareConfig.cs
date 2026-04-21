using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartParkingLot.Cli;

public sealed record SensorMapping(
    [property: JsonPropertyName("sensorId")]   string SensorId,
    [property: JsonPropertyName("spotId")]     string SpotId,
    [property: JsonPropertyName("actuatorId")] string ActuatorId,
    [property: JsonPropertyName("address")]    string Address  = "Sin dirección",
    [property: JsonPropertyName("type")]       string Type     = "Estándar",
    [property: JsonPropertyName("floor")]      string Floor    = "Planta Baja");

public sealed record HardwareConfig(
    [property: JsonPropertyName("port")]     string Port,
    [property: JsonPropertyName("baudRate")] int BaudRate,
    [property: JsonPropertyName("sensors")]  IReadOnlyList<SensorMapping> Sensors)
{
    public static HardwareConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Archivo de configuración de hardware no encontrado: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HardwareConfig>(json)
            ?? throw new InvalidOperationException("hardware.json no pudo deserializarse.");
    }

    public IReadOnlyDictionary<string, string> BuildSensorToSpot() =>
        Sensors.ToDictionary(m => m.SensorId, m => m.SpotId);

    public IReadOnlyDictionary<string, string> BuildSpotToActuator() =>
        Sensors.ToDictionary(m => m.SpotId, m => m.ActuatorId);
}
