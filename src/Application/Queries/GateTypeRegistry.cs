namespace SmartParkingLot.Application.Queries;

/// <summary>
/// Maps gate IDs to their human-readable type name (e.g. "ENTRY", "EXIT").
/// Registered by the GUI bootstrap from HardwareConfig so that queries
/// can project gate type without depending on the Hardware layer.
/// </summary>
public sealed class GateTypeRegistry
{
    private readonly IReadOnlyDictionary<string, string> _map;

    public GateTypeRegistry(IReadOnlyDictionary<string, string> gateIdToType)
    {
        _map = gateIdToType;
    }

    public string GetType(string gateId) =>
        _map.TryGetValue(gateId, out var t) ? t : "UNKNOWN";
}
