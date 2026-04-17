# Arduino Serial Bridge — Plan de Implementacion

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establecer comunicacion serial entre C# y Arduino para leer un sensor IR fisico y alimentar la capa de aplicacion del Smart Parking Lot.

**Architecture:** Un `ArduinoSerialBridge` (Pure Fabrication) en la capa Hardware lee datos seriales con formato `SENSOR_ID:VALOR` del Arduino, los parsea, y alimenta sensores existentes via la nueva interfaz `ISensorCapture<T>` (DIP). El sketch Arduino se modifica para enviar ese formato estructurado.

**Tech Stack:** C# 14 / .NET 10, System.IO.Ports (NuGet), Arduino (sketch C++)

**Spec:** `docs/superpowers/specs/2026-04-16-arduino-serial-bridge-design.md`

---

## File Map

| Accion | Archivo | Responsabilidad |
|--------|---------|-----------------|
| Create | `src/Core/Interfaces/ISensorCapture.cs` | Interfaz segregada para capturar lecturas (ISP + DIP) |
| Modify | `src/Hardware/Sensor.cs` | Implementar `ISensorCapture<T>` |
| Create | `src/Hardware/ArduinoSerialBridge.cs` | Pure Fabrication: comunicacion serial con Arduino |
| Modify | `src/Hardware/SmartParkingLot.Hardware.csproj` | Agregar NuGet `System.IO.Ports` |
| Modify | `src/Cli/Program.cs` | Integrar bridge en Composition Root |
| Create | `arduino/spot_sensor/spot_sensor.ino` | Sketch modificado con protocolo `ID:VALOR` |

---

### Task 1: Interfaz ISensorCapture\<T\>

**Files:**
- Create: `src/Core/Interfaces/ISensorCapture.cs`

- [ ] **Step 1: Crear la interfaz ISensorCapture\<T\>**

```csharp
using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Ports;

// SOLID - ISP: Separa la capacidad de capturar lecturas de la capacidad de leerlas (ISensor)
// SOLID - DIP: Permite que consumidores dependan de esta abstraccion, no del concreto Sensor<T>
public interface ISensorCapture<T> where T : SensorReading
{
    T CaptureReading(T reading);
}
```

- [ ] **Step 2: Verificar que compila**

Run: `dotnet build src/Core/SmartParkingLot.Core.csproj`
Expected: Build succeeded

---

### Task 2: Sensor\<T\> implementa ISensorCapture\<T\>

**Files:**
- Modify: `src/Hardware/Sensor.cs:8`

- [ ] **Step 1: Modificar la declaracion de clase de Sensor\<T\>**

Cambiar la linea 8 de:

```csharp
public class Sensor<T> : ISensor where T : SensorReading
```

a:

```csharp
public class Sensor<T> : ISensor, ISensorCapture<T> where T : SensorReading
```

No hay que modificar nada mas: el metodo `CaptureReading(T reading)` ya existe en la linea 35 y satisface el contrato de la interfaz.

- [ ] **Step 2: Verificar que compila**

Run: `dotnet build src/Hardware/SmartParkingLot.Hardware.csproj`
Expected: Build succeeded

---

### Task 3: Agregar NuGet System.IO.Ports

**Files:**
- Modify: `src/Hardware/SmartParkingLot.Hardware.csproj`

- [ ] **Step 1: Agregar el paquete NuGet**

Run: `dotnet add src/Hardware/SmartParkingLot.Hardware.csproj package System.IO.Ports`
Expected: El paquete se instala correctamente

- [ ] **Step 2: Verificar que compila**

Run: `dotnet build src/Hardware/SmartParkingLot.Hardware.csproj`
Expected: Build succeeded

---

### Task 4: ArduinoSerialBridge

**Files:**
- Create: `src/Hardware/ArduinoSerialBridge.cs`

- [ ] **Step 1: Crear la clase ArduinoSerialBridge**

```csharp
using System.IO.Ports;
using SmartParkingLot.Core;
using SmartParkingLot.Core.Ports;

namespace SmartParkingLot.Hardware;

// GRASP - Pure Fabrication: No existe en el dominio del parqueadero.
// Es infraestructura que traduce comunicacion serial a lecturas de dominio.
// SOLID - DIP: Depende de ISensorCapture<SpotSensorReading>, no del concreto Sensor<T>.
public class ArduinoSerialBridge : IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)> _sensorMap;
    private Thread? _readThread;
    private volatile bool _listening;

    /// <summary>
    /// Crea un bridge serial hacia Arduino.
    /// </summary>
    /// <param name="portName">Puerto serial (ej. "COM6")</param>
    /// <param name="baudRate">Velocidad en baudios (ej. 9600)</param>
    /// <param name="sensorMap">Mapeo: hardware ID -> (spot ID del dominio, sensor). El bridge traduce IDs de hardware a IDs de dominio.</param>
    public ArduinoSerialBridge(
        string portName,
        int baudRate,
        Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)> sensorMap)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _sensorMap = sensorMap;
    }

    /// <summary>
    /// Abre el puerto serial e inicia un hilo de fondo para lectura continua.
    /// </summary>
    public void StartListening()
    {
        try
        {
            _serialPort.Open();
            _listening = true;
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "ArduinoSerialBridge-Reader"
            };
            _readThread.Start();
            Console.WriteLine($"[ArduinoSerialBridge] Escuchando en {_serialPort.PortName} a {_serialPort.BaudRate} baud");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ArduinoSerialBridge] Error al abrir puerto {_serialPort.PortName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Detiene la lectura y cierra el puerto serial.
    /// </summary>
    public void StopListening()
    {
        _listening = false;

        if (_serialPort.IsOpen)
            _serialPort.Close();

        Console.WriteLine("[ArduinoSerialBridge] Escucha detenida");
    }

    private void ReadLoop()
    {
        while (_listening && _serialPort.IsOpen)
        {
            try
            {
                var line = _serialPort.ReadLine().Trim();
                ProcessLine(line);
            }
            catch (TimeoutException)
            {
                // Sin datos en este ciclo, continuar
            }
            catch (Exception ex)
            {
                if (_listening)
                    Console.WriteLine($"[ArduinoSerialBridge] Error de lectura: {ex.Message}");
            }
        }
    }

    private void ProcessLine(string line)
    {
        // Formato esperado: SENSOR_ID:VALOR (ej. "IR1:1")
        var parts = line.Split(':');

        if (parts.Length != 2)
        {
            Console.WriteLine($"[ArduinoSerialBridge] Formato invalido: '{line}'");
            return;
        }

        var sensorId = parts[0];
        var rawValue = parts[1];

        if (rawValue is not ("0" or "1"))
        {
            Console.WriteLine($"[ArduinoSerialBridge] Valor invalido para '{sensorId}': '{rawValue}' (esperado 0 o 1)");
            return;
        }

        if (!_sensorMap.TryGetValue(sensorId, out var entry))
        {
            Console.WriteLine($"[ArduinoSerialBridge] Sensor no mapeado: '{sensorId}'");
            return;
        }

        var isOccupied = rawValue == "1";
        // Se usa el spotId del dominio (ej. "A1"), no el hardware ID (ej. "IR1")
        var reading = new SpotSensorReading(entry.SpotId, isOccupied);
        entry.Sensor.CaptureReading(reading);

        Console.WriteLine($"[ArduinoSerialBridge] {sensorId} ({entry.SpotId}) -> {(isOccupied ? "OCUPADO" : "LIBRE")}");
    }

    public void Dispose()
    {
        StopListening();
        _serialPort.Dispose();
    }
}
```

- [ ] **Step 2: Verificar que compila**

Run: `dotnet build src/Hardware/SmartParkingLot.Hardware.csproj`
Expected: Build succeeded

---

### Task 5: Integrar bridge en Program.cs

**Files:**
- Modify: `src/Cli/Program.cs`

- [ ] **Step 1: Agregar integracion del ArduinoSerialBridge**

Despues del bloque de creacion de sensores (linea 24), agregar el mapeo y el bridge. Reemplazar el bloque de sensores simulados (lineas 21-24) con:

```csharp
// ── 2. Crear sensores (IoT) ──
var spotSensorA1 = new Sensor<SpotSensorReading>("SEN-SPOT-A1", "IR Arduino");

// ── 2.1 Bridge serial: conecta Arduino fisico con sensores via ISensorCapture (DIP) ──
// Mapeo: hardware ID -> (spot ID del dominio, sensor)
var sensorMap = new Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)>
{
    ["IR1"] = ("A1", spotSensorA1)
};

using var bridge = new ArduinoSerialBridge("COM6", 9600, sensorMap);
bridge.StartListening();
```

Agregar tambien el using necesario al inicio del archivo:

```csharp
using SmartParkingLot.Core.Ports;
```

**Nota:** El resto de la simulacion (gateSensor, otros spotSensors, fases 1-5) se mantiene intacta. El bridge opera en paralelo alimentando `spotSensorA1` con datos reales del Arduino.

- [ ] **Step 2: Verificar que compila**

Run: `dotnet build src/Cli/SmartParkingLot.Cli.csproj`
Expected: Build succeeded

- [ ] **Step 3: Ejecutar y verificar que el dato llega**

Run: `dotnet run --project src/Cli/SmartParkingLot.Cli.csproj`

Expected: En consola se deben ver las lecturas del Arduino intercaladas con la simulacion:
```
[ArduinoSerialBridge] Escuchando en COM6 a 9600 baud
[ArduinoSerialBridge] IR1 -> OCUPADO
[ArduinoSerialBridge] IR1 -> LIBRE
```

Si el Arduino no esta conectado, se vera:
```
[ArduinoSerialBridge] Error al abrir puerto COM6: ...
```

---

### Task 6: Sketch Arduino modificado

**Files:**
- Create: `arduino/spot_sensor/spot_sensor.ino`

- [ ] **Step 1: Crear el sketch con protocolo SENSOR_ID:VALOR**

```cpp
// Smart Parking Lot — Sensor IR de Spot
// Protocolo serial: SENSOR_ID:VALOR (ej. "IR1:1")
// VALOR: 1 = ocupado (objeto detectado), 0 = libre

const int IR1_PIN = 7;
const int LED_PIN = 13;

void setup() {
  pinMode(IR1_PIN, INPUT);
  pinMode(LED_PIN, OUTPUT);
  Serial.begin(9600);
}

void loop() {
  int value1 = digitalRead(IR1_PIN);

  // Sensor IR activo en LOW: LOW = objeto detectado = ocupado (1)
  bool occupied1 = (value1 == LOW);

  Serial.print("IR1:");
  Serial.println(occupied1 ? "1" : "0");

  // LED indicador: encendido si cualquier sensor detecta
  digitalWrite(LED_PIN, occupied1 ? HIGH : LOW);

  delay(2000);
}
```

- [ ] **Step 2: Verificar la estructura del sketch**

Run: `ls arduino/spot_sensor/`
Expected: `spot_sensor.ino`

**Nota:** Este sketch debe cargarse manualmente al Arduino via Arduino IDE. No se automatiza el upload desde C#.

---

### Task 7: Verificacion end-to-end

- [ ] **Step 1: Compilar solucion completa**

Run: `dotnet build`
Expected: Build succeeded, 0 errores

- [ ] **Step 2: Ejecutar con Arduino conectado en COM6**

Run: `dotnet run --project src/Cli/SmartParkingLot.Cli.csproj`

Verificar en consola:
1. `[ArduinoSerialBridge] Escuchando en COM6 a 9600 baud` — puerto abierto
2. `[ArduinoSerialBridge] IR1 (A1) -> OCUPADO` o `IR1 (A1) -> LIBRE` — datos parseados con spot ID del dominio
3. `[Sensor SEN-SPOT-A1] Captura registrada` — sensor recibe la lectura via ISensorCapture

Si se coloca un objeto frente al sensor IR, el estado debe cambiar de LIBRE a OCUPADO. Al retirarlo, vuelve a LIBRE.

- [ ] **Step 3: Verificar que el dato llega a la capa de aplicacion**

Agregar temporalmente despues de `bridge.StartListening()` en Program.cs:

```csharp
// Verificacion temporal: esperar lecturas del Arduino y actualizar CapacityService
Console.WriteLine("\n[Verificacion] Esperando 5 segundos para lecturas del Arduino...");
Thread.Sleep(5000);

var snapshot = spotSensorA1.GetSnapshot();
if (snapshot is not null)
{
    Console.WriteLine($"[Verificacion] Ultima lectura: {snapshot}");
    capacityService.UpdateSpotState(snapshot);
    Console.WriteLine($"[Verificacion] Espacios disponibles: {lot.AvailableSpots}");
}
else
{
    Console.WriteLine("[Verificacion] No se recibieron lecturas del Arduino");
}
```

Run: `dotnet run --project src/Cli/SmartParkingLot.Cli.csproj`

Expected:
```
[Verificacion] Esperando 5 segundos para lecturas del Arduino...
[ArduinoSerialBridge] IR1 (A1) -> OCUPADO
[Verificacion] Ultima lectura: [SpotSensorReading] Espacio: A1 | Ocupado: Si | Tiempo: ...
[CapacityService] Espacio 'A1' marcado como OCUPADO por sensor
```

**Nota:** Este bloque de verificacion temporal se elimina despues de confirmar que funciona.
