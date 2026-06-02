namespace SmartParkingLot.Application.Hardware;

/// <summary>
/// Validación pura de una configuración de hardware antes de persistirla.
/// Devuelve la lista de errores; vacía significa válida.
/// </summary>
public static class HardwareConfigValidator
{
    public static IReadOnlyList<string> Validate(HardwareConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.Port))
            errors.Add("El puerto no puede estar vacío.");

        if (config.BaudRate <= 0)
            errors.Add("El baudRate debe ser mayor que cero.");

        var spotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sensorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in config.Sensors)
        {
            if (string.IsNullOrWhiteSpace(s.SpotId))
                errors.Add("Hay un spot con Id vacío.");
            else if (!spotIds.Add(s.SpotId))
                errors.Add($"SpotId duplicado: '{s.SpotId}'.");

            if (string.IsNullOrWhiteSpace(s.SensorId))
                errors.Add($"El spot '{s.SpotId}' no tiene SensorId.");
            else if (!sensorIds.Add(s.SensorId))
                errors.Add($"SensorId duplicado: '{s.SensorId}'.");
        }

        var gateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in config.Gates)
        {
            if (string.IsNullOrWhiteSpace(g.GateId))
                errors.Add("Hay un gate con Id vacío.");
            else if (!gateIds.Add(g.GateId))
                errors.Add($"GateId duplicado: '{g.GateId}'.");

            if (g.Pin is < 0 or > 255)
                errors.Add($"Pin inválido en gate '{g.GateId}': {g.Pin} (rango 0-255).");
        }

        return errors;
    }
}
