using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

/// <summary>
/// Drop-in replacement for <see cref="ArduinoSerialBridge"/> used when
/// hardware.json has <c>"port": "MOCK"</c>.
/// Inherits from <see cref="ArduinoSerialBridge"/> so it satisfies every
/// DI registration that expects the concrete type (including ViewModels).
/// No serial port is ever opened.
/// </summary>
public sealed class MockArduinoBridge : ArduinoSerialBridge
{
    private const string LogSource = "MockArduinoBridge";
    private const int SIMULATION_INTERVAL_MS = 5000;

    private readonly IEventPublisher _events;
    private readonly ILogger _logger;
    private readonly IReadOnlyList<string> _sensorIds;
    private readonly Random _rng = new(42);

    private volatile bool _mockListening;
    private Thread? _simThread;

    public override bool IsListening => _mockListening;

    /// <summary>
    /// Creates a mock bridge that simulates sensor readings every ~5 seconds.
    /// </summary>
    /// <param name="events">Event bus used to publish <see cref="SensorReadingReceived"/>.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <param name="sensorIds">
    ///   Sensor IDs from hardware.json that the simulation will cycle through.
    /// </param>
    public MockArduinoBridge(
        IEventPublisher events,
        ILogger logger,
        IReadOnlyCollection<string> sensorIds)
        : base("MOCK", 9600, events, logger)   // base never opens the port
    {
        _events    = events;
        _logger    = logger;
        _sensorIds = sensorIds.Count > 0 ? sensorIds.ToList() : ["SEN-MOCK-01"];
    }

    public override void StartListening()
    {
        if (_mockListening) return;

        _mockListening = true;
        _simThread = new Thread(SimulationLoop)
        {
            IsBackground = true,
            Name = "MockArduinoBridge-Sim"
        };
        _simThread.Start();
        _logger.Info(LogSource, "Modo MOCK activo — simulando lecturas cada ~5 s");
    }

    public override void StopListening()
    {
        _mockListening = false;
        _logger.Info(LogSource, "Simulación MOCK detenida");
    }

    /// <summary>No-op: there is no physical serial port to write to.</summary>
    public override void WriteLine(string line) { /* intentionally empty */ }

    public override void Dispose()
    {
        StopListening();
        // Do NOT call base.Dispose() — base holds a real SerialPort("MOCK")
        // that was never opened; disposing it is safe, but avoids confusion.
    }

    // ── private ────────────────────────────────────────────────────────────

    private void SimulationLoop()
    {
        // Track per-sensor state so readings alternate occupied/free
        var states = _sensorIds.ToDictionary(id => id, _ => false);

        while (_mockListening)
        {
            Thread.Sleep(SIMULATION_INTERVAL_MS);
            if (!_mockListening) break;

            // Pick a random sensor
            var idx      = _rng.Next(_sensorIds.Count);
            var sensorId = _sensorIds[idx];

            // Toggle its state
            states[sensorId] = !states[sensorId];
            var rawValue = states[sensorId] ? "1" : "0";
            var stateStr = states[sensorId] ? "OCUPADO" : "LIBRE";

            _logger.Debug(LogSource, $"[MOCK] {sensorId} -> {stateStr}");

            _events.Publish(new SensorReadingReceived(
                SensorId:   sensorId,
                SensorType: "SENSOR",
                RawValue:   rawValue,
                Timestamp:  DateTimeOffset.UtcNow));
        }
    }
}
