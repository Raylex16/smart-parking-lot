# Spec: Cableado LCD + Integración Cámaras OV7670 con Tesseract OCR

**Fecha:** 2026-05-17  
**Proyecto:** Smart Parking Lot (.NET 10 / C# 14 / Arduino Mega)  
**Alcance:** Documentación de cableado físico (LCD I2C y cámara OV7670) + implementación de reconocimiento real de placas vehiculares reemplazando `PlaceholderPlateRecognizer`.

---

## 1. Contexto y motivación

El sistema ya tiene una capa de software completa para la pantalla LCD (`LcdDisplay.cs`, `LcdCapacityHandler.cs`) y un contrato de reconocimiento de placas (`ILicensePlateRecognizer`), pero:

- El cableado físico del LCD nunca estuvo documentado en el repositorio.
- `PlaceholderPlateRecognizer` devuelve placas falsas (`AUTO-{timestamp}`), lo que impide usar políticas de acceso basadas en placa real (`RestrictedAccessPolicy`, whitelist).
- No existe soporte para capturar imágenes desde el Arduino.

Este spec cubre ambos aspectos: el documento de cableado y la integración de código.

---

## 2. Cableado físico — `docs/hardware-wiring.md`

### 2.1 Pantalla LCD I2C 16×2

Módulo LCD con backpack PCF8574 (dirección I2C `0x27`). Se conecta al bus `Wire` del Arduino Mega (I2C0).

```
Módulo I2C LCD       Arduino Mega 2560
  VCC    ──────────→  5V
  GND    ──────────→  GND
  SDA    ──────────→  Pin 20  (SDA / Wire)
  SCL    ──────────→  Pin 21  (SCL / Wire)
```

**Notas:**
- El LCD ya está funcional en el sketch `spot_sensor.ino` vía librería `LiquidCrystal_I2C`.
- Si hay múltiples dispositivos en el mismo bus I2C (LCD + sensores), se deben usar resistencias pull-up de 4.7 kΩ en SDA y SCL hacia 5V.
- No requiere ningún cambio de software; `LcdDisplay.cs` ya envía `CMD:ACT:LCD:STATUS` y `CMD:ACT:LCD:MSG`.

### 2.2 Cámara OV7670 (una por puerta)

El OV7670 usa dos interfaces:
- **SCCB** (configuración, I2C-compatible): se conecta a `Wire1` del Mega (I2C1) para no colisionar con el LCD en `Wire` (I2C0).
- **Bus de datos paralelo 8 bits** (píxeles): pines 22–29 (Port A del Mega, lectura rápida con `PINA`).

```
OV7670              Arduino Mega 2560
  3.3V  ──────────→  3.3V  ⚠ NO conectar a 5V
  GND   ──────────→  GND
  SDA   ──────────→  Pin 70  (SDA1 / Wire1)
  SCL   ──────────→  Pin 71  (SCL1 / Wire1)
  D0    ──────────→  Pin 22  (PA0)
  D1    ──────────→  Pin 23  (PA1)
  D2    ──────────→  Pin 24  (PA2)
  D3    ──────────→  Pin 25  (PA3)
  D4    ──────────→  Pin 26  (PA4)
  D5    ──────────→  Pin 27  (PA5)
  D6    ──────────→  Pin 28  (PA6)
  D7    ──────────→  Pin 29  (PA7)
  VSYNC ──────────→  Pin 2   (INT0, interrupción externa)
  HREF  ──────────→  Pin 3   (INT1)
  PCLK  ──────────→  Pin 4   (entrada digital, cada pulso = 1 píxel)
  XCLK  ──────────→  Pin 11  (OC1A, PWM 8 MHz como clock del sensor)
  RESET ──────────→  Pin 32  (activo bajo)
  PWDN  ──────────→  Pin 33  (activo alto = apagado; mantener LOW)
```

**Diagrama de conexión I2C con LCD (bus compartido Wire)**
```
Arduino Mega
    20 (SDA) ──┬── 4.7kΩ ── 5V
               ├── LCD I2C (0x27)
               └── (solo LCD en Wire; OV7670 va a Wire1)

    21 (SCL) ──┬── 4.7kΩ ── 5V
               └── LCD I2C (0x27)

    70 (SDA1) ─┬── OV7670 SDA  (sin pull-up externo, Wire1 los tiene internos)
    71 (SCL1) ─┴── OV7670 SCL
```

**Notas OV7670:**
- El sensor funciona a **3.3 V lógico**. Los pines de datos del Mega son 5V. Se recomienda un divisor resistivo (1kΩ / 2kΩ) en cada línea D0–D7 y VSYNC/HREF/PCLK para bajar de 5V a 3.3V. O bien usar un nivel shifter genérico 4-channel.
- `XCLK` requiere una señal de 8–24 MHz. Pin 11 (OC1A) configurado en modo PWM 50% duty cycle a 8 MHz.
- La dirección SCCB del OV7670 es `0x42` (write) / `0x43` (read), equivalente a `0x21` en Wire.

**Resolución usada:** QQVGA (160×120) en escala de grises = 19 200 bytes por frame.

---

## 3. Protocolo serial de captura de imagen

El Arduino Mega ya comunica con C# por serial (9600 baud para comandos). La transferencia de imagen requiere **115 200 baud** para evitar latencias de 30+ segundos. Se usará codificación **base64 por líneas** (compatible con el parser de líneas existente en `ArduinoSerialBridge`).

### 3.1 Flujo de captura

```
C#                                        Arduino
  │  CMD:CAM:CAPTURE:G-01               →   │
  │                                          │  captura frame OV7670 QQVGA gray
  │  ←  CAM:BEGIN:19200                      │  (19 200 bytes)
  │  ←  CAM:DATA:SGVsbG8gV29ybGQ...          │  (chunks base64 ~60 chars/línea)
  │  ←  CAM:DATA:...                         │  (~344 líneas para 19 200 bytes)
  │  ←  CAM:END                              │
```

### 3.2 Mensajes del protocolo

| Dirección    | Formato                    | Descripción                                   |
|--------------|----------------------------|-----------------------------------------------|
| C# → Arduino | `CMD:CAM:CAPTURE:{gateId}` | Solicita captura para la puerta indicada       |
| Arduino → C# | `CAM:BEGIN:{byteCount}:{gateId}` | Inicia transferencia; byteCount = bytes totales, gateId para correlacionar |
| Arduino → C# | `CAM:DATA:{base64Chunk}`   | Chunk de datos (~45 bytes decodificados/línea)  |
| Arduino → C# | `CAM:END`                  | Fin de frame; imagen completa disponible        |
| Arduino → C# | `CAM:ERROR:{reason}`       | Fallo en captura (timeout, sensor no listo)     |

### 3.3 Tiempo estimado de transferencia

- Frame QQVGA gray: 19 200 bytes → base64: ~25 600 chars
- A 115 200 baud (11 520 bytes/s): ~2.2 segundos por frame
- Aceptable para detección de placas al paso

---

## 4. Diseño de software

### 4.1 Cambio de interfaz: `ILicensePlateRecognizer` → async

**Archivo:** `src/Core/Interfaces/ILicensePlateRecognizer.cs`

La interfaz actual es síncrona. La captura + OCR tarda ~3–5 segundos; bloquear el hilo de eventos es inaceptable.

```csharp
// ANTES
public interface ILicensePlateRecognizer
{
    string Recognize(string gateId);
}

// DESPUÉS
public interface ILicensePlateRecognizer
{
    Task<string> RecognizeAsync(string gateId, CancellationToken ct = default);
}
```

Todos los implementadores (`PlaceholderPlateRecognizer`, nuevo `TesseractPlateRecognizer`) y el consumidor (`GateSensorHandler`) se actualizan en consecuencia.

### 4.2 Nuevo evento de dominio: `CameraFrameReceived`

**Archivo:** `src/Core/Events/CameraFrameReceived.cs`

```csharp
public record CameraFrameReceived(string GateId, byte[] ImageBytes);
```

`ArduinoSerialBridge` parsea las líneas `CAM:BEGIN`, `CAM:DATA`, `CAM:END` y publica este evento cuando el frame está completo. Nada más en el sistema necesita saber del protocolo base64.

### 4.3 Modificación de `ArduinoSerialBridge`

**Archivo:** `src/Hardware/ArduinoSerialBridge.cs`

Se agrega estado interno para acumular el frame:

```
_cameraState:
  - Idle
  - Receiving(expectedBytes, StringBuilder base64Buffer, string gateId)
```

Al recibir `CAM:BEGIN:{n}:G-01` → pasa a `Receiving`.  
Al recibir `CAM:DATA:{chunk}` → append al buffer.  
Al recibir `CAM:END` → decode base64 → publica `CameraFrameReceived(gateId, bytes)`.  
Al recibir `CAM:ERROR` → publica `CameraFrameReceived(gateId, Array.Empty<byte>())` (frame vacío = fallo).

### 4.4 Nuevo servicio: `OV7670FrameReader`

**Archivo:** `src/Application/Recognition/OV7670FrameReader.cs`  
**Interfaz nueva:** `src/Application/Recognition/ICameraCapture.cs`

```csharp
public interface ICameraCapture
{
    Task<byte[]> CaptureAsync(string gateId, CancellationToken ct = default);
}
```

`OV7670FrameReader` implementa `ICameraCapture`:
1. Envía `CMD:CAM:CAPTURE:{gateId}` vía `ICommandDispatcher`.
2. Crea un `TaskCompletionSource<byte[]>` con timeout de 10 segundos.
3. Suscribe al bus de eventos escuchando `CameraFrameReceived` donde `GateId == gateId`.
4. Cuando el evento llega, resuelve el TCS y desuscribe.
5. Si el TCS expira, cancela y lanza `TimeoutException`.

### 4.5 Nuevo implementador: `TesseractPlateRecognizer`

**Archivo:** `src/Application/Recognition/TesseractPlateRecognizer.cs`  
**NuGet:** `Tesseract` v5.x (ya disponible en NuGet; requiere `tessdata/` con `eng.traineddata`)

```
TesseractPlateRecognizer
  ├── ICameraCapture _camera
  └── string _tessDataPath

RecognizeAsync(gateId):
  1. bytes = await _camera.CaptureAsync(gateId)
  2. Si bytes vacíos → return "UNKNOWN"
  3. using var pix = Pix.LoadFromMemory(bytes, width:160, height:120, depth:8)
  4. using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default)
  5. engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-")
  6. using var page = engine.Process(pix)
  7. plate = page.GetText().Trim()
  8. return string.IsNullOrWhiteSpace(plate) ? "UNKNOWN" : plate
```

**Preprocesamiento de imagen:** El frame llega como bytes grayscale raw (no PNG). Tesseract necesita formato reconocible. Se creará un `Bitmap` de 160×120 con `PixelFormat.Format8bppIndexed` a partir de los bytes raw antes de pasárselo a Tesseract.

**Modelo Tesseract:** Para placas colombianas (formato `ABC-123`), `eng.traineddata` base es suficiente con el whitelist. Un modelo entrenado específicamente para placas mejoraría la precisión pero no es requerido en esta iteración.

### 4.6 `PlaceholderPlateRecognizer` actualizado

La firma cambia a async pero mantiene la lógica fake:

```csharp
public Task<string> RecognizeAsync(string gateId, CancellationToken ct = default)
    => Task.FromResult($"AUTO-{DateTime.Now:HHmmssfff}");
```

### 4.7 `GateSensorHandler` actualizado

**Archivo:** `src/Application/Handlers/GateSensorHandler.cs`

El método `HandleAsync` ya es `async`; solo cambia la llamada de:
```csharp
var plate = _plateRecognizer.Recognize(gateId);
```
a:
```csharp
var plate = await _plateRecognizer.RecognizeAsync(gateId, ct);
```

### 4.8 Sketch Arduino actualizado

**Archivo:** `arduino/spot_sensor/spot_sensor.ino`

Cambios:
1. Subir baudrate de `9600` a `115200` (también en `hardware.json` y `CliConstants`).
2. Agregar inicialización OV7670 via `Wire1` al arrancar.
3. En el parser de comandos, reconocer `CMD:CAM:CAPTURE:{gateId}`:
   - Configurar OV7670 en modo captura (QQVGA grayscale).
   - Leer frame completo usando VSYNC + HREF + PCLK + Port A.
   - Codificar en base64 y enviar líneas `CAM:BEGIN`, `CAM:DATA`, `CAM:END`.

### 4.9 `hardware.json` actualizado

Se agrega campo `cameras` que mapea `gateId → {sensorId, pin}`:

```json
{
  "port": "COM5",
  "baudRate": 115200,
  "sensors": [...],
  "gates": [...],
  "cameras": [
    { "gateId": "G-01", "sensorId": "CAM-01" },
    { "gateId": "G-02", "sensorId": "CAM-02" }
  ],
  "manualApprovalTimeoutSeconds": 15
}
```

### 4.10 Wiring en `ApplicationModule`

- Registrar `ICameraCapture` → `OV7670FrameReader` (singleton).
- Registrar `ILicensePlateRecognizer` → `TesseractPlateRecognizer` (singleton) con path a `tessdata/`.
- `tessdata/` se copia a `OutputDirectory` con `<Content CopyToOutputDirectory="PreserveNewest">`.

---

## 5. Archivos a crear / modificar

| Acción    | Archivo                                                    |
|-----------|------------------------------------------------------------|
| **Crear** | `docs/hardware-wiring.md`                                  |
| **Crear** | `src/Core/Events/CameraFrameReceived.cs`                   |
| **Crear** | `src/Application/Recognition/ICameraCapture.cs`            |
| **Crear** | `src/Application/Recognition/OV7670FrameReader.cs`         |
| **Crear** | `src/Application/Recognition/TesseractPlateRecognizer.cs`  |
| **Crear** | `tessdata/eng.traineddata` (descargado del repo oficial)   |
| **Modificar** | `src/Core/Interfaces/ILicensePlateRecognizer.cs` — async   |
| **Modificar** | `src/Application/Recognition/PlaceholderPlateRecognizer.cs`|
| **Modificar** | `src/Application/Handlers/GateSensorHandler.cs`           |
| **Modificar** | `src/Hardware/ArduinoSerialBridge.cs` — parsear CAM:*      |
| **Modificar** | `arduino/spot_sensor/spot_sensor.ino` — OV7670 + 115200    |
| **Modificar** | `src/Cli/hardware.json` — baudRate + cameras               |
| **Modificar** | `src/GUI/hardware.json` — ídem                             |
| **Modificar** | `src/Application/Bootstrap/ApplicationModule.cs` — wiring  |
| **Modificar** | `src/Application/Bootstrap/ApplicationModule.cs` — agregar `TessDataPath` al record `ApplicationOptions` |
| **Modificar** | `src/Application/SmartParkingLot.Application.csproj` — NuGet Tesseract |

---

## 6. Verificación

1. `dotnet build` — 0 errores tras los cambios de interfaz.
2. Con `PlaceholderPlateRecognizer` aún activo en modo mock: el flujo de entrada sigue funcionando igual que antes (placa fake, gate se abre).
3. Con `TesseractPlateRecognizer` activo: enviar imagen de prueba (`test-plate.png`) y verificar que Tesseract retorna una cadena no vacía.
4. Modo integración con Arduino: conectar OV7670, ejecutar CLI, simular sensor IR de puerta y verificar que:
   - El Arduino envía `CAM:BEGIN` / `CAM:DATA` / `CAM:END`.
   - C# recibe el evento `CameraFrameReceived`.
   - Tesseract procesa la imagen y devuelve una placa (puede ser impreciso en condiciones de laboratorio).
   - El gate se abre/bloquea según la política de acceso.
5. `Select-String` en `src/Cli/**/*.cs` y `src/GUI/**/*.cs` — sin referencias a `SmartParkingLot.Hardware` ni `SmartParkingLot.Core` directas nuevas.
