# Arduino Bidirectional Communication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Evolucionar el canal serial Arduino ↔ .NET de unidireccional (sensor → dominio) a bidireccional (dominio → actuador), manteniendo limpia la separación entre capas.

**Architecture:** Core define contratos (eventos inbound, comandos outbound, puertos `IEventPublisher` / `ICommandDispatcher`). Application orquesta reglas de negocio vía handlers suscritos a eventos de dominio. Hardware implementa los puertos: parser/writer serial con cola de comandos y loop lector. CLI sólo compone DI.

**Tech Stack:** C# 14, .NET 10, `System.IO.Ports`, xUnit (nuevo proyecto de tests), sketch Arduino existente extendido con parser de comandos.

---

## Contexto arquitectónico (original)

Ver sección final **"Diseño de referencia (brainstorm inicial)"** para la estrategia completa: contratos, protocolo, flujo end-to-end y criterios de aceptación. Las tareas de este plan implementan ese diseño.

**Protocolo serial (V1):**
- Inbound evento:  `EVT:SENSOR:<sensorId>:<value>`  (ej. `EVT:SENSOR:IR1:1`)
- Outbound comando: `CMD:ACT:<actuatorId>:<action>:<payload>`  (ej. `CMD:ACT:LED1:SET:1`)
- Ack / Nack:       `ACK:<commandId>` / `NACK:<commandId>:<reason>`

**Compatibilidad:** el parser inbound actual acepta `IR1:1` sin prefijo. Se mantendrá compatibilidad en Fase 2 (acepta ambos) y se migrará el sketch en Fase 5.

---

## File Structure

Nuevos archivos (agrupados por responsabilidad, no por capa técnica):

**Core — contratos**
- `src/Core/Events/SensorReadingReceived.cs` — evento inbound tipado
- `src/Core/Events/SpotOccupancyChanged.cs` — evento de dominio
- `src/Core/Commands/ActuatorCommand.cs` — comando outbound (record)
- `src/Core/Interfaces/IEventPublisher.cs` — puerto publish/subscribe in-process
- `src/Core/Interfaces/ICommandDispatcher.cs` — puerto envío comandos a hardware

**Application — casos de uso**
- `src/Application/UseCases/HandleSensorReadingUseCase.cs` — procesa lectura, actualiza dominio
- `src/Application/Handlers/SpotOccupancyChangedHandler.cs` — traduce evento de dominio a comando de actuador
- `src/Application/Infrastructure/InProcessEventBus.cs` — implementación simple de `IEventPublisher`

**Hardware — serial bidireccional**
- `src/Hardware/SerialProtocol.cs` — parser y serializer (puro, testeable)
- `src/Hardware/SerialCommandDispatcher.cs` — implementa `ICommandDispatcher` con cola y writer loop
- `src/Hardware/ArduinoSerialBridge.cs` — **modificar**: ahora publica `SensorReadingReceived` por `IEventPublisher` y delega escritura a dispatcher

**ParkingSpot — evento de dominio**
- `src/Core/Entities/ParkingSpot.cs` — **modificar**: emitir `SpotOccupancyChanged` cuando cambia ocupación

**CLI — composición**
- `src/Cli/Program.cs` — **modificar**: eliminar loop de monitoreo con lógica de negocio; cableado de bus + handlers

**Tests (nuevos)**
- `tests/SmartParkingLot.Tests/SmartParkingLot.Tests.csproj`
- `tests/SmartParkingLot.Tests/Hardware/SerialProtocolTests.cs`
- `tests/SmartParkingLot.Tests/Application/HandleSensorReadingUseCaseTests.cs`
- `tests/SmartParkingLot.Tests/Application/SpotOccupancyChangedHandlerTests.cs`
- `tests/SmartParkingLot.Tests/Infrastructure/InProcessEventBusTests.cs`

**Sketch Arduino**
- `arduino/smart_parking_sketch.ino` — **modificar**: emitir `EVT:SENSOR:...`, leer `CMD:ACT:...`, responder `ACK:`

---

## Task 0: Setup proyecto de tests

**Files:**
- Create: `tests/SmartParkingLot.Tests/SmartParkingLot.Tests.csproj`
- Modify: solución raíz (`*.sln` si existe) o añadir referencia en build

- [ ] **Step 1: Crear proyecto xUnit**

```bash
mkdir -p tests/SmartParkingLot.Tests
cd tests/SmartParkingLot.Tests
dotnet new xunit -n SmartParkingLot.Tests --force
dotnet add reference ../../src/Core/SmartParkingLot.Core.csproj
dotnet add reference ../../src/Application/SmartParkingLot.Application.csproj
dotnet add reference ../../src/Hardware/SmartParkingLot.Hardware.csproj
```

- [ ] **Step 2: Añadir al .sln si existe (opcional)**

```bash
cd ../..
[ -f *.sln ] && dotnet sln add tests/SmartParkingLot.Tests/SmartParkingLot.Tests.csproj || true
```

- [ ] **Step 3: Verificar build + run vacío**

Run: `dotnet test tests/SmartParkingLot.Tests/SmartParkingLot.Tests.csproj`
Expected: compila, 0 tests ejecutados, exit 0.

- [ ] **Step 4: Commit**

```bash
git add tests/ *.sln 2>/dev/null
git commit -m "chore: add xUnit test project"
```

---

## Task 1: Contratos Core (eventos y comandos)

**Files:**
- Create: `src/Core/Events/SensorReadingReceived.cs`
- Create: `src/Core/Events/SpotOccupancyChanged.cs`
- Create: `src/Core/Commands/ActuatorCommand.cs`

- [ ] **Step 1: Definir `SensorReadingReceived`**

```csharp
namespace SmartParkingLot.Core.Events;

// GRASP - Pure Fabrication: contrato inbound desacoplado de transporte serial.
public sealed record SensorReadingReceived(
    string SensorId,
    string SensorType,
    string RawValue,
    DateTimeOffset Timestamp);
```

- [ ] **Step 2: Definir `SpotOccupancyChanged`**

```csharp
namespace SmartParkingLot.Core.Events;

public sealed record SpotOccupancyChanged(
    string SpotId,
    bool IsOccupied,
    string Source,
    DateTimeOffset Timestamp);
```

- [ ] **Step 3: Definir `ActuatorCommand`**

```csharp
namespace SmartParkingLot.Core.Commands;

public sealed record ActuatorCommand(
    string CommandId,
    string ActuatorId,
    string Action,
    string Payload);
```

- [ ] **Step 4: Compilar**

Run: `dotnet build src/Core/SmartParkingLot.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Events src/Core/Commands
git commit -m "feat(core): add event/command contracts for bidirectional flow"
```

---

## Task 2: Puertos `IEventPublisher` y `ICommandDispatcher`

**Files:**
- Create: `src/Core/Interfaces/IEventPublisher.cs`
- Create: `src/Core/Interfaces/ICommandDispatcher.cs`

- [ ] **Step 1: `IEventPublisher`**

```csharp
namespace SmartParkingLot.Core.Interfaces;

public interface IEventPublisher
{
    void Publish<TEvent>(TEvent @event) where TEvent : notnull;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
}
```

- [ ] **Step 2: `ICommandDispatcher`**

```csharp
using SmartParkingLot.Core.Commands;

namespace SmartParkingLot.Core.Interfaces;

public interface ICommandDispatcher
{
    void Dispatch(ActuatorCommand command);
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/Core/SmartParkingLot.Core.csproj
git add src/Core/Interfaces
git commit -m "feat(core): add IEventPublisher and ICommandDispatcher ports"
```

---

## Task 3: `InProcessEventBus` con tests (TDD)

**Files:**
- Test: `tests/SmartParkingLot.Tests/Infrastructure/InProcessEventBusTests.cs`
- Create: `src/Application/Infrastructure/InProcessEventBus.cs`

- [ ] **Step 1: Test falla — entrega a suscriptor del mismo tipo**

```csharp
using SmartParkingLot.Application.Infrastructure;
using Xunit;

namespace SmartParkingLot.Tests.Infrastructure;

public sealed record FooEvent(int N);

public class InProcessEventBusTests
{
    [Fact]
    public void Publish_delivers_event_to_matching_subscriber()
    {
        var bus = new InProcessEventBus();
        FooEvent? received = null;
        bus.Subscribe<FooEvent>(e => received = e);

        bus.Publish(new FooEvent(42));

        Assert.Equal(new FooEvent(42), received);
    }

    [Fact]
    public void Publish_does_not_deliver_to_other_type_subscribers()
    {
        var bus = new InProcessEventBus();
        var called = false;
        bus.Subscribe<FooEvent>(_ => called = true);

        bus.Publish("no-soy-foo");

        Assert.False(called);
    }

    [Fact]
    public void Multiple_subscribers_all_receive_event()
    {
        var bus = new InProcessEventBus();
        var count = 0;
        bus.Subscribe<FooEvent>(_ => count++);
        bus.Subscribe<FooEvent>(_ => count++);

        bus.Publish(new FooEvent(1));

        Assert.Equal(2, count);
    }
}
```

- [ ] **Step 2: Run tests — deben fallar (no existe la clase)**

Run: `dotnet test --filter FullyQualifiedName~InProcessEventBusTests`
Expected: FAIL — `InProcessEventBus` no existe.

- [ ] **Step 3: Implementación mínima**

```csharp
using System.Collections.Concurrent;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Infrastructure;

public sealed class InProcessEventBus : IEventPublisher
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Delegate>());
        lock (list) list.Add(handler);
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : notnull
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
        Delegate[] snapshot;
        lock (list) snapshot = list.ToArray();
        foreach (var h in snapshot) ((Action<TEvent>)h)(@event);
    }
}
```

- [ ] **Step 4: Run tests — verdes**

Run: `dotnet test --filter FullyQualifiedName~InProcessEventBusTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Application/Infrastructure tests/SmartParkingLot.Tests/Infrastructure
git commit -m "feat(app): in-process event bus with subscriber registry"
```

---

## Task 4: `SerialProtocol` parser/serializer puro (TDD)

**Files:**
- Test: `tests/SmartParkingLot.Tests/Hardware/SerialProtocolTests.cs`
- Create: `src/Hardware/SerialProtocol.cs`

- [ ] **Step 1: Escribir tests**

```csharp
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
```

- [ ] **Step 2: Run — falla por compilación**

Run: `dotnet test --filter FullyQualifiedName~SerialProtocolTests`
Expected: FAIL (tipo inexistente).

- [ ] **Step 3: Implementar `SerialProtocol`**

```csharp
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
```

- [ ] **Step 4: Run — verde**

Run: `dotnet test --filter FullyQualifiedName~SerialProtocolTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Hardware/SerialProtocol.cs tests/SmartParkingLot.Tests/Hardware
git commit -m "feat(hardware): pure SerialProtocol parser/serializer with tests"
```

---

## Task 5: Refactor `ArduinoSerialBridge` a inbound vía `IEventPublisher`

**Files:**
- Modify: `src/Hardware/ArduinoSerialBridge.cs`

- [ ] **Step 1: Cambiar ctor — recibir `IEventPublisher` en vez de `sensorMap`**

Reemplazar campos y constructor por:

```csharp
private readonly SerialPort _serialPort;
private readonly IEventPublisher _events;
private Thread? _readThread;
private volatile bool _listening;

public ArduinoSerialBridge(string portName, int baudRate, IEventPublisher events)
{
    _serialPort = new SerialPort(portName, baudRate);
    _serialPort.ReadTimeout = SERIAL_TIMEOUT_MS;
    _serialPort.WriteTimeout = SERIAL_TIMEOUT_MS;
    _events = events;
}
```

- [ ] **Step 2: Reemplazar `ProcessLine` por parser robusto con compatibilidad**

```csharp
private void ProcessLine(string line)
{
    // Formato nuevo: EVT:SENSOR:<id>:<value>
    if (SerialProtocol.TryParseEvent(line, out var evt))
    {
        _events.Publish(evt!);
        return;
    }

    // Compat V0: <id>:<value>  (ej. "IR1:1")
    var parts = line.Split(':');
    if (parts.Length == 2 && parts[1] is "0" or "1")
    {
        _events.Publish(new SensorReadingReceived(parts[0], "SENSOR", parts[1], DateTimeOffset.UtcNow));
        return;
    }

    Console.WriteLine($"[ArduinoSerialBridge] Linea ignorada: '{line}'");
}
```

- [ ] **Step 3: Exponer método `WriteLine(string)` interno para dispatcher**

```csharp
internal void WriteLine(string line)
{
    if (!_serialPort.IsOpen) return;
    _serialPort.WriteLine(line);
}
```

- [ ] **Step 4: Compilar — romperá `Program.cs`, dejar así; arreglado en Task 9**

Run: `dotnet build src/Hardware/SmartParkingLot.Hardware.csproj`
Expected: PASS.

Run: `dotnet build`
Expected: FAIL en `Cli` (señal correcta).

- [ ] **Step 5: Commit**

```bash
git add src/Hardware/ArduinoSerialBridge.cs
git commit -m "refactor(hardware): bridge publishes SensorReadingReceived via IEventPublisher"
```

---

## Task 6: `ParkingSpot` emite `SpotOccupancyChanged`

**Files:**
- Modify: `src/Core/Entities/ParkingSpot.cs`

- [ ] **Step 1: Leer archivo actual**

Run: `cat src/Core/Entities/ParkingSpot.cs`
(inspeccionar el método que aplica el estado — probablemente `SetOccupied` o similar).

- [ ] **Step 2: Añadir evento C# estándar**

En la entidad:

```csharp
public event Action<SpotOccupancyChanged>? OccupancyChanged;
```

- [ ] **Step 3: Disparar sólo ante cambio de estado**

Localizar el setter/método que cambia `IsOccupied`. Sustituir por:

```csharp
public void ApplyOccupancy(bool isOccupied, string source)
{
    if (IsOccupied == isOccupied) return;  // idempotencia
    IsOccupied = isOccupied;
    OccupancyChanged?.Invoke(
        new SpotOccupancyChanged(Id, isOccupied, source, DateTimeOffset.UtcNow));
}
```

Nota: si el código actual tiene otro nombre (`MarkOccupied` / `MarkFree`), unificarlo en `ApplyOccupancy` y actualizar llamadores en `CapacityService`.

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: posibles errores en callers — corregir llamadas en `CapacityService.UpdateSpotState` para invocar `ApplyOccupancy(isOccupied, "sensor")`.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Entities/ParkingSpot.cs src/Application/Services/CapacityService.cs
git commit -m "feat(core): ParkingSpot emits SpotOccupancyChanged on state change"
```

---

## Task 7: `HandleSensorReadingUseCase` (TDD)

**Files:**
- Test: `tests/SmartParkingLot.Tests/Application/HandleSensorReadingUseCaseTests.cs`
- Create: `src/Application/UseCases/HandleSensorReadingUseCase.cs`

- [ ] **Step 1: Test**

```csharp
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using Xunit;

namespace SmartParkingLot.Tests.Application;

public class HandleSensorReadingUseCaseTests
{
    [Fact]
    public void Reading_1_marks_mapped_spot_as_occupied()
    {
        var lot = new ParkingLot("LOT", "X", ParkingMode.AUTOMATIC);
        lot.AddSpot(new ParkingSpot("A1", "zona", "std", "pb"));
        var uc = new HandleSensorReadingUseCase(lot, new Dictionary<string,string>{["IR1"]="A1"});

        uc.Handle(new SensorReadingReceived("IR1","SENSOR","1", DateTimeOffset.UtcNow));

        Assert.True(lot.GetSpots().Single(s => s.Id == "A1").IsOccupied);
    }

    [Fact]
    public void Unmapped_sensor_is_ignored()
    {
        var lot = new ParkingLot("LOT", "X", ParkingMode.AUTOMATIC);
        lot.AddSpot(new ParkingSpot("A1", "zona", "std", "pb"));
        var uc = new HandleSensorReadingUseCase(lot, new Dictionary<string,string>());

        uc.Handle(new SensorReadingReceived("IR9","SENSOR","1", DateTimeOffset.UtcNow));

        Assert.False(lot.GetSpots().Single(s => s.Id == "A1").IsOccupied);
    }
}
```

- [ ] **Step 2: Run — FAIL**

Run: `dotnet test --filter FullyQualifiedName~HandleSensorReadingUseCaseTests`
Expected: FAIL.

- [ ] **Step 3: Implementar caso de uso**

```csharp
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;

namespace SmartParkingLot.Application.UseCases;

public sealed class HandleSensorReadingUseCase
{
    private readonly ParkingLot _lot;
    private readonly IReadOnlyDictionary<string, string> _sensorToSpot;

    public HandleSensorReadingUseCase(ParkingLot lot, IReadOnlyDictionary<string, string> sensorToSpot)
    {
        _lot = lot;
        _sensorToSpot = sensorToSpot;
    }

    public void Handle(SensorReadingReceived evt)
    {
        if (!_sensorToSpot.TryGetValue(evt.SensorId, out var spotId)) return;
        if (evt.RawValue is not ("0" or "1")) return;
        var spot = _lot.GetSpots().FirstOrDefault(s => s.Id == spotId);
        spot?.ApplyOccupancy(evt.RawValue == "1", $"sensor:{evt.SensorId}");
    }
}
```

- [ ] **Step 4: Run — PASS + commit**

```bash
dotnet test --filter FullyQualifiedName~HandleSensorReadingUseCaseTests
git add src/Application/UseCases tests/SmartParkingLot.Tests/Application
git commit -m "feat(app): HandleSensorReadingUseCase updates domain from inbound events"
```

---

## Task 8: `SerialCommandDispatcher` con cola + writer loop

**Files:**
- Create: `src/Hardware/SerialCommandDispatcher.cs`

- [ ] **Step 1: Implementación**

```csharp
using System.Collections.Concurrent;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Hardware;

public sealed class SerialCommandDispatcher : ICommandDispatcher, IDisposable
{
    private readonly ArduinoSerialBridge _bridge;
    private readonly BlockingCollection<ActuatorCommand> _queue = new();
    private readonly Thread _writer;
    private volatile bool _running = true;

    public SerialCommandDispatcher(ArduinoSerialBridge bridge)
    {
        _bridge = bridge;
        _writer = new Thread(Loop) { IsBackground = true, Name = "SerialCommandDispatcher-Writer" };
        _writer.Start();
    }

    public void Dispatch(ActuatorCommand command) => _queue.Add(command);

    private void Loop()
    {
        foreach (var cmd in _queue.GetConsumingEnumerable())
        {
            if (!_running) break;
            try
            {
                _bridge.WriteLine(SerialProtocol.SerializeCommand(cmd));
                Console.WriteLine($"[Dispatcher] -> {cmd.CommandId} {cmd.ActuatorId} {cmd.Action}:{cmd.Payload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dispatcher] Error enviando {cmd.CommandId}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _queue.CompleteAdding();
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Hardware/SerialCommandDispatcher.cs
git commit -m "feat(hardware): SerialCommandDispatcher with background writer queue"
```

---

## Task 9: `SpotOccupancyChangedHandler` (TDD)

**Files:**
- Test: `tests/SmartParkingLot.Tests/Application/SpotOccupancyChangedHandlerTests.cs`
- Create: `src/Application/Handlers/SpotOccupancyChangedHandler.cs`

- [ ] **Step 1: Test con `ICommandDispatcher` fake**

```csharp
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using Xunit;

namespace SmartParkingLot.Tests.Application;

public class SpotOccupancyChangedHandlerTests
{
    sealed class FakeDispatcher : ICommandDispatcher
    {
        public List<ActuatorCommand> Sent { get; } = new();
        public void Dispatch(ActuatorCommand c) => Sent.Add(c);
    }

    [Fact]
    public void Occupied_spot_sends_LED_ON()
    {
        var fake = new FakeDispatcher();
        var h = new SpotOccupancyChangedHandler(fake, new Dictionary<string,string>{["A1"]="LED1"});

        h.Handle(new SpotOccupancyChanged("A1", true, "sensor:IR1", DateTimeOffset.UtcNow));

        var cmd = Assert.Single(fake.Sent);
        Assert.Equal("LED1", cmd.ActuatorId);
        Assert.Equal("SET", cmd.Action);
        Assert.Equal("1", cmd.Payload);
    }

    [Fact]
    public void Free_spot_sends_LED_OFF()
    {
        var fake = new FakeDispatcher();
        var h = new SpotOccupancyChangedHandler(fake, new Dictionary<string,string>{["A1"]="LED1"});

        h.Handle(new SpotOccupancyChanged("A1", false, "sensor:IR1", DateTimeOffset.UtcNow));

        Assert.Equal("0", fake.Sent.Single().Payload);
    }
}
```

- [ ] **Step 2: Implementación**

```csharp
using SmartParkingLot.Core.Commands;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Handlers;

public sealed class SpotOccupancyChangedHandler
{
    private readonly ICommandDispatcher _dispatcher;
    private readonly IReadOnlyDictionary<string, string> _spotToActuator;

    public SpotOccupancyChangedHandler(
        ICommandDispatcher dispatcher,
        IReadOnlyDictionary<string, string> spotToActuator)
    {
        _dispatcher = dispatcher;
        _spotToActuator = spotToActuator;
    }

    public void Handle(SpotOccupancyChanged evt)
    {
        if (!_spotToActuator.TryGetValue(evt.SpotId, out var actuatorId)) return;
        _dispatcher.Dispatch(new ActuatorCommand(
            CommandId: Guid.NewGuid().ToString("N")[..8],
            ActuatorId: actuatorId,
            Action: "SET",
            Payload: evt.IsOccupied ? "1" : "0"));
    }
}
```

- [ ] **Step 3: Verde + commit**

```bash
dotnet test --filter FullyQualifiedName~SpotOccupancyChangedHandlerTests
git add src/Application/Handlers tests/SmartParkingLot.Tests/Application
git commit -m "feat(app): handler translates domain event to actuator command"
```

---

## Task 10: Cablear en `Program.cs` (Composition Root)

**Files:**
- Modify: `src/Cli/Program.cs`

- [ ] **Step 1: Reemplazar Program.cs por composición completa**

```csharp
using SmartParkingLot.Application;
using SmartParkingLot.Application.Handlers;
using SmartParkingLot.Application.Infrastructure;
using SmartParkingLot.Application.UseCases;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Events;
using SmartParkingLot.Core.Interfaces;
using SmartParkingLot.Hardware;

// 1. Dominio
var lot = new ParkingLot("LOT-01", "Campus Barcelona", ParkingMode.AUTOMATIC);
lot.AddSpot(new ParkingSpot("A1", "Zona-A Fila-1", "Estándar", "Planta Baja"));

// 2. Bus in-process
IEventPublisher bus = new InProcessEventBus();

// 3. Hardware: bridge (inbound) + dispatcher (outbound)
using var bridge = new ArduinoSerialBridge(DEFAULT_PORT_NAME, DEFAULT_BAUD_RATE, bus);
using var dispatcher = new SerialCommandDispatcher(bridge);

// 4. Caso de uso: lecturas -> dominio
var sensorToSpot = new Dictionary<string, string> { ["IR1"] = "A1" };
var handleReading = new HandleSensorReadingUseCase(lot, sensorToSpot);
bus.Subscribe<SensorReadingReceived>(handleReading.Handle);

// 5. Handler: eventos de dominio -> comandos
var spotToActuator = new Dictionary<string, string> { ["A1"] = "LED1" };
var occupancyHandler = new SpotOccupancyChangedHandler(dispatcher, spotToActuator);

// Suscribir el evento de dominio de cada spot al bus global
foreach (var spot in lot.GetSpots())
    spot.OccupancyChanged += occupancyHandler.Handle;

// 6. Arrancar
bridge.StartListening();

Console.WriteLine("Smart Parking Lot — bidireccional OK. Ctrl+C para salir.");
// Loop vivo sin polling de dominio: eventos hacen el trabajo.
var done = new ManualResetEventSlim();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
done.Wait();
```

- [ ] **Step 2: Build + run smoke**

Run: `dotnet build`
Expected: Build succeeded.

Run: `dotnet run --project src/Cli/SmartParkingLot.Cli.csproj` (manual, con Arduino conectado) — verificar que una lectura `EVT:SENSOR:IR1:1` produce log de dispatcher `LED1 SET:1`.

Si no hay Arduino disponible, simular enviando por serial virtual o saltar al Task 11 (sketch).

- [ ] **Step 3: Commit**

```bash
git add src/Cli/Program.cs
git commit -m "feat(cli): wire event bus + command dispatcher in composition root"
```

---

## Task 11: Sketch Arduino — emitir `EVT:` y consumir `CMD:`

**Files:**
- Modify: `arduino/smart_parking_sketch.ino` (ajustar ruta si difiere)

- [ ] **Step 1: Añadir emisión con prefijo + lectura de comandos**

Reemplazar el bucle principal por algo como:

```c
const int IR1_PIN = 2;
const int LED1_PIN = 9;
int lastIR1 = -1;
String inBuf = "";

void setup() {
  pinMode(IR1_PIN, INPUT);
  pinMode(LED1_PIN, OUTPUT);
  Serial.begin(9600);
}

void loop() {
  int v = digitalRead(IR1_PIN);
  if (v != lastIR1) {
    lastIR1 = v;
    Serial.print("EVT:SENSOR:IR1:");
    Serial.println(v);
  }

  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n') { handleCommand(inBuf); inBuf = ""; }
    else if (c != '\r') inBuf += c;
  }
  delay(20);
}

void handleCommand(const String& line) {
  // Formato: CMD:ACT:<id>:SET:<0|1>
  if (!line.startsWith("CMD:ACT:")) return;
  int idEnd = line.indexOf(':', 8);
  if (idEnd < 0) { Serial.println("NACK:?:malformed"); return; }
  String actId = line.substring(8, idEnd);
  int actionEnd = line.indexOf(':', idEnd + 1);
  String action = line.substring(idEnd + 1, actionEnd);
  String payload = line.substring(actionEnd + 1);

  if (actId == "LED1" && action == "SET") {
    digitalWrite(LED1_PIN, payload == "1" ? HIGH : LOW);
    Serial.println("ACK:LED1");
  } else {
    Serial.println("NACK:LED1:unsupported");
  }
}
```

- [ ] **Step 2: Flashear Arduino y verificar end-to-end**

Con el CLI corriendo: cubrir/descubrir el sensor IR → el LED debería encender/apagar. Observar `ACK:` en los logs si se añade lectura de ack en `ArduinoSerialBridge.ProcessLine` (opcional en fase posterior).

- [ ] **Step 3: Commit**

```bash
git add arduino/
git commit -m "feat(arduino): emit EVT: and consume CMD: with ACK/NACK"
```

---

## Task 12: Aceptación + limpieza

- [ ] **Step 1: Criterios de aceptación**

Verificar manualmente:
- `Program.cs` no contiene lógica de negocio (sólo composición y `Console.WriteLine` banner).
- Cambio de ocupación impacta dominio vía `HandleSensorReadingUseCase`.
- LED funcional con ACK visible.
- Añadir actuador motor sería: ampliar `ActuatorCommand` payload + entry en `spotToActuator` + branch en sketch (sin tocar entidades).

- [ ] **Step 2: Run full test suite**

Run: `dotnet test`
Expected: todos los tests en verde.

- [ ] **Step 3: Commit final (si hay cambios pendientes)**

```bash
git status
git add -A && git commit -m "chore: finalize bidirectional arduino integration" || true
```

---

## Fases diferidas (fuera de este plan)

Del diseño original, se dejan explícitamente fuera y se documentan como trabajo futuro:

- **Fase 6 — Hardening:** retries, timeouts, deduplicación de comandos, idempotencia, reconexión automática del puerto serial, parseo y correlación de ACK/NACK con `CommandId`, simulador serial para tests de integración. Abrir plan separado cuando haya un caso de uso real que lo justifique (YAGNI).
- **Múltiples spots/actuadores:** el diseño ya los soporta por diccionarios; expandir mapeos en `Program.cs` cuando el hardware lo requiera.

---

## Diseño de referencia (brainstorm inicial)

> Esta sección preserva el razonamiento arquitectónico que originó el plan. Las tareas de arriba implementan este diseño.

**Capas y responsabilidades**

- **Core:** entidades (`ParkingSpot`, `ParkingLot`), eventos de dominio (`SpotOccupancyChanged`), puertos (`ICommandDispatcher`, `IEventPublisher`).
- **Application:** casos de uso (`HandleSensorReadingUseCase`) y handlers (`SpotOccupancyChangedHandler`). Regla: "si spot ocupado ⇒ actuador X". Orquesta eventos sin conocer serial.
- **Hardware:** `ArduinoSerialBridge` bidireccional (reader loop + writer queue), traductor protocolo texto ↔ mensajes tipados, drivers de actuador (LED hoy, motor mañana).
- **CLI:** sólo composición DI y arranque.

**Flujo end-to-end objetivo**

1. Arduino envía `EVT:SENSOR:IR1:1`.
2. Bridge parsea y publica `SensorReadingReceived`.
3. `HandleSensorReadingUseCase` actualiza `ParkingSpot`.
4. `ParkingSpot` emite `SpotOccupancyChanged`.
5. `SpotOccupancyChangedHandler` lo traduce a `ActuatorCommand`.
6. `SerialCommandDispatcher` envía `CMD:ACT:LED1:SET:1`.
7. Arduino ejecuta y responde `ACK:`.

**Complejidad:** media. **Riesgo principal:** confiabilidad del canal serial (orden, ACK, reconexión) — mitigado difiriendo hardening a Fase 6.
