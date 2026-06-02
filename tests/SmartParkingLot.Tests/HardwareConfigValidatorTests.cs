using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core;

namespace SmartParkingLot.Tests;

public class HardwareConfigValidatorTests
{
    private static HardwareConfig Valid() => new(
        Port: "COM5",
        BaudRate: 115200,
        Sensors: new[]
        {
            new SensorMapping("IR1", "A-01", "LED1", "Zona A", "Estándar", "Planta 1"),
            new SensorMapping("IR2", "A-02", "LED2", "Zona A", "Estándar", "Planta 1")
        },
        Gates: new[] { new GateMapping("G-01", GateType.ENTRY, "GATE-IR1", "GATE1", 9) });

    [Fact]
    public void Validate_ValidConfig_NoErrors()
    {
        Assert.Empty(HardwareConfigValidator.Validate(Valid()));
    }

    [Fact]
    public void Validate_DuplicateSpotId_ReportsError()
    {
        var dup = Valid() with
        {
            Sensors = new[]
            {
                new SensorMapping("IR1", "A-01", "LED1", "Zona A", "Estándar", "Planta 1"),
                new SensorMapping("IR2", "A-01", "LED2", "Zona A", "Estándar", "Planta 1")
            }
        };
        Assert.Contains(HardwareConfigValidator.Validate(dup), e => e.Contains("A-01"));
    }

    [Fact]
    public void Validate_EmptyPort_ReportsError()
    {
        var bad = Valid() with { Port = "" };
        Assert.Contains(HardwareConfigValidator.Validate(bad), e => e.Contains("puerto"));
    }

    [Fact]
    public void Validate_InvalidPin_ReportsError()
    {
        var bad = Valid() with
        {
            Gates = new[] { new GateMapping("G-01", GateType.ENTRY, "GATE-IR1", "GATE1", 999) }
        };
        Assert.Contains(HardwareConfigValidator.Validate(bad), e => e.Contains("Pin"));
    }
}
