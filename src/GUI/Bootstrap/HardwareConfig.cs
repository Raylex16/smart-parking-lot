using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartParkingLot.Core;

namespace SmartParkingLot.Gui.Bootstrap;

public sealed record SensorMapping(
    [property: JsonPropertyName("sensorId")]   string SensorId,
    [property: JsonPropertyName("spotId")]     string SpotId,
    [property: JsonPropertyName("actuatorId")] string ActuatorId,
    [property: JsonPropertyName("address")]    string Address = "Sin dirección",
    [property: JsonPropertyName("type")]       string Type    = "Estándar",
    [property: JsonPropertyName("floor")]      string Floor   = "Planta Baja");

public sealed record GateMapping(
    [property: JsonPropertyName("gateId")]     string GateId,
    [property: JsonPropertyName("type")]       GateType Type,
    [property: JsonPropertyName("irSensorId")] string IrSensorId,
    [property: JsonPropertyName("actuatorId")] string ActuatorId,
    [property: JsonPropertyName("pin")]        int Pin);

public sealed record HardwareConfig(
    [property: JsonPropertyName("port")]     string Port,
    [property: JsonPropertyName("baudRate")] int BaudRate,
    [property: JsonPropertyName("sensors")]  IReadOnlyList<SensorMapping> Sensors,
    [property: JsonPropertyName("gates")]    IReadOnlyList<GateMapping> Gates)
{
    public static HardwareConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"hardware.json no encontrado: {path}");

        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HardwareConfig>(json, options)
            ?? throw new InvalidOperationException("hardware.json no pudo deserializarse.");
    }

    public IReadOnlyDictionary<string, string> BuildSensorToSpot() =>
        Sensors.ToDictionary(m => m.SensorId, m => m.SpotId);

    public IReadOnlyDictionary<string, string> BuildSpotToActuator() =>
        Sensors.ToDictionary(m => m.SpotId, m => m.ActuatorId);

    public IReadOnlyDictionary<string, (string GateId, GateType Type)> BuildGateSensorMapping() =>
        Gates.ToDictionary(g => g.IrSensorId, g => (g.GateId, g.Type));

    public IReadOnlyDictionary<string, string> BuildGateActuatorMapping() =>
        Gates.ToDictionary(g => g.GateId, g => g.ActuatorId);
}
