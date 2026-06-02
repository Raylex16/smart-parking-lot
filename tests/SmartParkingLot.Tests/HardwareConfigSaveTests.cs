using SmartParkingLot.Application.Hardware;
using SmartParkingLot.Core;

namespace SmartParkingLot.Tests;

public class HardwareConfigSaveTests
{
    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var config = new HardwareConfig(
            Port: "COM7",
            BaudRate: 9600,
            Sensors: new[]
            {
                new SensorMapping("IR1", "A-01", "LED1", "Zona A, P. 1", "Estándar", "Planta 1")
            },
            Gates: new[]
            {
                new GateMapping("G-01", GateType.ENTRY, "GATE-IR1", "GATE1", 9)
            },
            ManualApprovalTimeoutSeconds: 20,
            AllowedPlates: new[] { "ABC-123" },
            Cameras: null);

        var path = Path.Combine(Path.GetTempPath(), $"hw-{Guid.NewGuid():N}.json");
        try
        {
            config.Save(path);
            var loaded = HardwareConfig.Load(path);

            Assert.Equal("COM7", loaded.Port);
            Assert.Equal(9600, loaded.BaudRate);
            Assert.Equal(20, loaded.ManualApprovalTimeoutSeconds);
            Assert.Single(loaded.Sensors);
            Assert.Equal("A-01", loaded.Sensors[0].SpotId);
            Assert.Single(loaded.Gates);
            Assert.Equal(GateType.ENTRY, loaded.Gates[0].Type);
            Assert.Equal(9, loaded.Gates[0].Pin);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
