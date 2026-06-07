namespace SmartParkingLot.Core.Entities;

public class AccessPolicyConfig
{
    public string LotId { get; private set; }
    public IReadOnlyList<string> AllowedPlates => _plates.AsReadOnly();
    public TimeSpan ScheduleStart { get; private set; } = TimeSpan.FromHours(8);
    public TimeSpan ScheduleEnd   { get; private set; } = TimeSpan.FromHours(20);

    private List<string> _plates = [];

    private AccessPolicyConfig() { LotId = ""; }

    public AccessPolicyConfig(string lotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotId);
        LotId = lotId;
    }

    public void SetWhitelist(IEnumerable<string> plates)
    {
        _plates = plates
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public void SetSchedule(TimeSpan start, TimeSpan end)
    {
        ScheduleStart = start;
        ScheduleEnd   = end;
    }
}
