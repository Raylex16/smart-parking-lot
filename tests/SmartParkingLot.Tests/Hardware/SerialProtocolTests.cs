using SmartParkingLot.Core.Commands;
using SmartParkingLot.Hardware;
using Xunit;

namespace SmartParkingLot.Tests.Hardware;

public class SerialProtocolTests
{
    [Theory]
    [InlineData("EVT:SENSOR:IR1:1", "IR1", "SENSOR", "1")]
    [InlineData("EVT:SENSOR:IR2:0", "IR2", "SENSOR", "0")]
    public void ParseEvent_returns_typed_reading(string line, string id, string type, string value)
    {
        var result = SerialProtocol.TryParseEvent(line, out var evt);
        Assert.True(result);
        Assert.Equal(id, evt!.SensorId);
        Assert.Equal(type, evt.SensorType);
        Assert.Equal(value, evt.RawValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NOPE")]
    [InlineData("EVT:SENSOR:IR1")]
    [InlineData("CMD:ACT:LED1:SET:1")]
    public void ParseEvent_rejects_invalid(string line)
    {
        Assert.False(SerialProtocol.TryParseEvent(line, out _));
    }

    [Fact]
    public void SerializeCommand_formats_expected_line()
    {
        var cmd = new ActuatorCommand("c-1", "LED1", "SET", "1");
        Assert.Equal("CMD:ACT:LED1:SET:1", SerialProtocol.SerializeCommand(cmd));
    }

    [Theory]
    [InlineData("ACK:c-1", true, "c-1", null)]
    [InlineData("NACK:c-1:timeout", false, "c-1", "timeout")]
    public void ParseAck_extracts_status(string line, bool ok, string id, string? reason)
    {
        Assert.True(SerialProtocol.TryParseAck(line, out var ack));
        Assert.Equal(ok, ack!.Ok);
        Assert.Equal(id, ack.CommandId);
        Assert.Equal(reason, ack.Reason);
    }
}
