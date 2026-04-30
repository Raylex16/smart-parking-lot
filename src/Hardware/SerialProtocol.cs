using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Events;

namespace SmartParkingLot.Hardware;

public sealed record AckMessage(bool Ok, string CommandId, string? Reason);

public static class SerialProtocol
{
    public static bool TryParseEvent(string line, out SensorReadingReceived? evt)
    {
        evt = null;
        if (string.IsNullOrWhiteSpace(line)) return false;
        var parts = line.Split(':');
        if (parts.Length != 4 || parts[0] != "EVT") return false;
        evt = new SensorReadingReceived(parts[2], parts[1], parts[3], DateTimeOffset.UtcNow);
        return true;
    }

    public static string SerializeCommand(ActuatorCommand cmd)
        => $"CMD:ACT:{cmd.ActuatorId}:{cmd.Action}:{cmd.Payload}";

    public static bool TryParseAck(string line, out AckMessage? ack)
    {
        ack = null;
        if (string.IsNullOrWhiteSpace(line)) return false;
        var parts = line.Split(':');
        if (parts.Length >= 2 && parts[0] == "ACK")
        {
            ack = new AckMessage(true, parts[1], null);
            return true;
        }
        if (parts.Length >= 3 && parts[0] == "NACK")
        {
            ack = new AckMessage(false, parts[1], string.Join(':', parts[2..]));
            return true;
        }
        return false;
    }
}
