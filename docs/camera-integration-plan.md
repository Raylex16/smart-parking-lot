# Plan — Integración de cámaras OV7670+FIFO para LPR

## Contexto

El sistema hoy reconoce vehículos por sensor IR en cada puerta y usa una placa autogenerada (`AUTO-{ts}`) vía `PlaceholderPlateRecognizer`. La meta es reemplazar ese stub por **lectura real de placas** usando 2 cámaras OV7670 con FIFO AL422 (una en cada puerta) y OCR en el host.

El reemplazo es transparente: ya tenemos `ILicensePlateRecognizer` como Strategy. Sólo cambia la implementación inyectada en el Composition Root.

### Restricciones reconocidas

- **Latencia**: cada captura completa (frame + transferencia + OCR) ≈ **3-5 segundos**. Aceptable para demo, no para producción real.
- **Resolución**: limitada a QQVGA (160×120) o QVGA (320×240) por el ancho de banda serial.
- **OCR no es perfecto**: con preprocesado y placas bien iluminadas, ~70-85% de acierto típicamente. Habrá fallos; el sistema debe degradar elegantemente (loggear, alertar, usar placa fallback).

---

## Arquitectura objetivo

```
Vehículo en puerta entrada
        │
        ▼
[IR sensor GATE-IR1 → 1]
        │
        ▼ (Mega → serial USB)
EVT:SENSOR:GATE-IR1:1
        │
        ▼
GateSensorHandler.HandleAsync(evt)
        │
        ├─ logger.Info("Vehículo detectado en G-01...")
        │
        ▼
ILicensePlateRecognizer.RecognizeAsync("G-01")
        │
        └─ Ov7670PlateRecognizer:
            │ 1. Despachar CMD:ACT:CAM1:CAPTURE al Mega
            │ 2. Esperar EVT:FRAME:CAM1:<bytes binarios> con timeout
            │ 3. Decodificar bytes → Bitmap (RGB565 → RGB24)
            │ 4. Pre-procesar (grayscale, threshold, deskew, crop) con OpenCvSharp
            │ 5. OCR con Tesseract → string placa
            │ 6. Loggear placa reconocida
            │ 7. Return placa
        │
        ▼
EntryRequest construido con placa REAL
        │
        ▼
GateController.HandleRequest → política, capacidad, abrir puerta
```

**Latencia estimada por etapa (cámara entrada, QQVGA 160×120 RGB565):**

| Etapa | Tiempo |
|---|---|
| Trigger captura → frame en FIFO | ~0.5 s |
| FIFO → Mega → serial al PC (38 KB @ 250000 baud) | ~1.5 s |
| Decode y preprocesado (OpenCvSharp) | ~0.3 s |
| Tesseract OCR | ~0.5 s |
| **Total** | **~2.8 s** |

A QVGA (320×240) sumar ~4 s extra de transferencia → ~7 s total. Empezamos en QQVGA.

---

## Hardware: cableado y asignación de pines

### Pines actualmente en uso (Mega)
- D3, D4: GATE-IR1, GATE-IR2 (sensores puertas)
- D5, D6, D7: IR1, IR2, IR3 (sensores spots)
- D11, D12, D13: LED1, LED2, LED3 (indicadores spots)
- D20, D21: I2C (LCD + slave Uno)

### Cambios en pines existentes
**LEDs deben moverse** porque el Timer1 (que controla pin 11) lo necesitamos para generar XCLK de las cámaras (~8 MHz):

| Antes | Después |
|---|---|
| LED1 = D13 | LED1 = D22 |
| LED2 = D12 | LED2 = D23 |
| LED3 = D11 | LED3 = D24 |

Si añadimos los 3 spots nuevos (que ya planeamos):

| Nuevo | Pin |
|---|---|
| IR4 | D8 |
| IR5 | D14 |
| IR6 | D15 |
| LED4 | D25 |
| LED5 | D26 |
| LED6 | D27 |

### Pines nuevos para las cámaras

**Compartidos por ambas cámaras** (ahorra pines: solo se lee una a la vez):
| Función | Pin Mega |
|---|---|
| FIFO Data D0-D7 | D30, D31, D32, D33, D34, D35, D36, D37 |
| SCCB SIOC (clock) | D38 |
| SCCB SIOD (data) | D39 |
| XCLK (8 MHz desde Timer1) | D11 (OC1A) |

**Por cámara (CAM1 = entrada, CAM2 = salida):**

| Función | CAM1 | CAM2 |
|---|---|---|
| VSYNC (interrupt frame sync) | D2 (INT4) | D18 (INT5) |
| WRST (write reset) | D40 | D45 |
| RRST (read reset) | D41 | D46 |
| RCLK (FIFO read clock) | D42 | D47 |
| OE (FIFO output enable) | D43 | D48 |
| RESET (camera reset, para SCCB selector) | D44 | D49 |

**PWDN** y otras: típicamente fijos al GND/5V según el módulo.

### Conflicto SCCB (importante)

OV7670 tiene dirección SCCB fija = 0x42 (write) / 0x43 (read). Dos cámaras = mismo addr = conflicto.

**Solución**: durante init, mantener una cámara en RESET (LOW) mientras se configura la otra. Una vez ambas configuradas, ambas RESET en HIGH → guardan su estado interno. La selección de qué cámara responde después se hace por **OE** (output enable del FIFO).

```cpp
// Pseudocódigo init
digitalWrite(CAM2_RESET, LOW);   // CAM2 en reset
configureCameraSCCB();            // configura CAM1
digitalWrite(CAM2_RESET, HIGH);   // libera CAM2
delay(100);
digitalWrite(CAM1_RESET, LOW);
configureCameraSCCB();            // configura CAM2
digitalWrite(CAM1_RESET, HIGH);
```

### Alimentación

OV7670 + FIFO consume ~50-70 mA cada uno. Total 2 cámaras = ~140 mA. **Logica del Mega = 250 + 140 = ~400 mA**. Margen suficiente con la fuente actual.

**Cuidado con voltaje**: el OV7670 es 3.3V, el Mega es 5V. Las salidas del Mega (XCLK, SCCB, RESET) **podrían dañar la cámara** si no hay level shifters. Algunos módulos OV7670 traen level shifters integrados (chequear el módulo); si no, conviene usar resistencias divisoras o un módulo TXS0108E. La FIFO suele ser 5V tolerant en los pines de salida pero NO en los de entrada.

**Antes de empezar la Fase 1**: confirmar si tu módulo tiene level shifters (los más comunes en kits Arduino sí los traen).

---

## Fase 1 — Sketch del Mega con captura de UNA cámara

### Objetivo
Sólo CAM1 cableada. Comando manual `CMD:ACT:CAM1:CAPTURE` desde Serial Monitor del Mega → Mega captura un frame y lo emite por serial. Test offline (sin C# todavía).

### Cambios

**Sketch `arduino/spot_sensor/spot_sensor.ino`:**
- `#include` para SCCB bit-banging (no usar Wire — está ocupado por LCD/slave).
- Setup: configurar XCLK en D11 vía Timer1 (~8 MHz cuadrada), inicializar SCCB, configurar OV7670 (registros para QQVGA RGB565), fijar RESET en HIGH.
- Nuevo handler en `handleCommand` para `CMD:ACT:CAM<n>:CAPTURE`:
  1. WRST → LOW → HIGH (reset write pointer FIFO)
  2. WR enable (alguna versión del módulo lo controla por VSYNC interno, otras necesitan WEN explícito)
  3. Esperar VSYNC bajada-subida (1 frame) → desactivar WR
  4. RRST → LOW → HIGH (reset read pointer)
  5. OE → LOW (habilita output del FIFO)
  6. Loop: para cada pixel (38400 bytes en QQVGA RGB565):
     - RCLK pulse
     - Leer D0-D7
     - `Serial.write(byte)`
  7. OE → HIGH
  8. `Serial.println("ACK:CAM1")`

### Protocolo de frame por serial

**Texto + binario híbrido**:
```
EVT:FRAME:CAM1:START:<width>:<height>:<format>:<sizeBytes>\n
<sizeBytes bytes binarios crudos>
EVT:FRAME:CAM1:END\n
```

El header en texto le dice al PC qué viene. El body binario va sin escapar.

### Test de Fase 1

1. Cablear sólo CAM1 al Mega.
2. Subir sketch.
3. Abrir Serial Monitor a 250000 baud.
4. Enviar `CMD:ACT:CAM1:CAPTURE`.
5. Debería aparecer el header `EVT:FRAME:CAM1:START:160:120:RGB565:38400` seguido de basura binaria y `EVT:FRAME:CAM1:END`.
6. Para verificar visualmente, hacer un script Python `tools/decode_frame.py` que:
   - Lea el output del serial
   - Extraiga los bytes entre START y END
   - Convierta RGB565 → PNG con PIL
   - Abrir el PNG y ver si la imagen tiene sentido

**Criterio de éxito Fase 1**: el PNG generado se ve como la escena enfocada (aunque sea borrosa o con colores raros).

---

## Fase 2 — Bridge binario en C# y dump a archivo

### Objetivo
La app C# captura el frame y lo guarda como `.png` en `frames/`. Sin OCR todavía. Verificable visualmente.

### Cambios

**`src/Hardware/ArduinoSerialBridge.cs`:**
- Hoy lee línea por línea (`ReadLine`). Para frame binario hay que cambiar a modo binario:
  - Detectar `EVT:FRAME:CAM<n>:START:...` en texto
  - Cambiar a modo binario: leer N bytes (sizeBytes del header)
  - Volver a modo texto buscando `EVT:FRAME:CAM<n>:END\n`
- Publicar nuevo evento `FrameReceived(string CamId, int Width, int Height, byte[] PixelData, DateTimeOffset Timestamp)`.

**Nuevo evento `src/Core/Events/FrameReceived.cs`**.

**Nuevo handler `src/Application/Handlers/FrameDumpHandler.cs`** (temporal, sólo Fase 2):
- Suscrito a `FrameReceived`
- Decodifica RGB565 → bitmap RGB24 (con `System.Drawing.Common` o `SixLabors.ImageSharp`)
- Guarda como `frames/CAM<n>_<timestamp>.png`

**ConsoleMenu opción 12**: "Capturar frame de cámara" — pide CAM1 o CAM2, despacha el CMD, espera el evento.

### Test de Fase 2

1. Levantar app, menú opción 12 → CAM1.
2. Esperar ~2 segundos.
3. Ver el archivo `frames/CAM1_<ts>.png` generado y abrirlo.

**Criterio de éxito Fase 2**: PNG visualmente reconocible.

---

## Fase 3 — OCR con Tesseract

### Objetivo
Reemplazar `PlaceholderPlateRecognizer` por `Ov7670PlateRecognizer` que captura, preprocesa y reconoce.

### Cambios

**Dependencias NuGet** (en `src/Application/SmartParkingLot.Application.csproj`):
- `Tesseract` (4.x, .NET wrapper)
- `OpenCvSharp4` + `OpenCvSharp4.runtime.win` (preprocesado)

**Datos de Tesseract**: descargar `tessdata` (idioma `eng` para placas alfanuméricas — placas colombianas/españolas son ASCII estándar). Path típico: `tessdata/eng.traineddata`. Bundlear con la app.

**Refactor `ILicensePlateRecognizer`**:
```csharp
public interface ILicensePlateRecognizer
{
    Task<string> RecognizeAsync(string gateId, CancellationToken ct = default);
}
```
- `PlaceholderPlateRecognizer.RecognizeAsync` devuelve `Task.FromResult($"AUTO-{...}")`.

**Nueva implementación `src/Application/Recognition/Ov7670PlateRecognizer.cs`:**
- Inyecta `ICommandDispatcher`, `IEventSubscriber`, `ILogger`
- Mapeo `gateId → camId` (G-01 → CAM1, G-02 → CAM2)
- `RecognizeAsync(gateId)`:
  1. Suscribir TaskCompletionSource al próximo `FrameReceived` para esa CAM
  2. Despachar `CMD:ACT:CAM<n>:CAPTURE`
  3. Await TCS con timeout de 8 s
  4. Decodificar RGB565 → Mat de OpenCvSharp
  5. Pipeline preprocesado:
     - Convert grayscale
     - Gaussian blur 3×3
     - Otsu threshold (binarización adaptiva)
     - Morphology (cerrado para juntar caracteres)
     - Detectar ROI rectangular (placa) por contornos
     - Recortar y reescalar
  6. Pasar a Tesseract con whitelist `ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789`
  7. Limpiar resultado (regex para formato típico de placa)
  8. Loggear placa reconocida + confidence
  9. Si la confianza es baja o no matchea regex, devolver `UNKNOWN-{ts}` (igual permite tracking)
  10. Return string

**Refactor handlers a async**:
- `GateSensorHandler.Handle` → `HandleAsync` (Task)
- En `bus.Subscribe<SensorReadingReceived>(...)`: el bus actual es sync. Adaptar a:
  ```csharp
  bus.Subscribe<SensorReadingReceived>(evt => _ = gateSensorHandler.HandleAsync(evt));
  ```
  (Fire-and-forget con manejo de excepciones internas.)

**Wiring en `ParkingLotApp`**:
- Reemplazar `new PlaceholderPlateRecognizer()` por `new Ov7670PlateRecognizer(dispatcher, bus, logger)`.
- Mantener `PlaceholderPlateRecognizer` como fallback configurable (env var o flag).

### Test de Fase 3

1. Imprimir una placa de prueba en papel ("ABC123" en fuente grande).
2. Apuntar la cámara a la placa.
3. Menú opción 11 → G-01.
4. Esperar ~3 segundos.
5. Verificar log: `[Ov7670PlateRecognizer] Placa reconocida: ABC123 (confidence 87%)`.
6. Si reconoce mal, ajustar:
   - Distancia/iluminación de la placa
   - Whitelist de caracteres
   - Pipeline de preprocesado

**Criterio de éxito Fase 3**: ≥70% de aciertos en placas claras, bien iluminadas, perpendiculares a la cámara.

---

## Fase 4 — Segunda cámara + flujo end-to-end

### Objetivo
Cablear CAM2, mapear G-02 → CAM2, validar tránsito completo entrada+salida.

### Cambios

**Sketch del Mega**: extender el handler para `CAM2:CAPTURE` reusando todo el código de captura (parametrizar por índice de cámara).

**`Ov7670PlateRecognizer`**: el mapeo `gateId → camId` ya está parametrizable. Sólo agregar entrada para G-02.

**Concurrencia**: el bus de datos de las cámaras es físicamente compartido. Si dos vehículos disparan IR a la vez, las capturas deben serializarse. Implementar con un `SemaphoreSlim(1, 1)` dentro de `Ov7670PlateRecognizer`. La segunda solicitud espera a que termine la primera.

### Test de Fase 4

1. Disparar IR de G-01 y G-02 con menos de 1 segundo de diferencia.
2. Verificar que las dos placas se reconocen secuencialmente (no en paralelo) y ambas puertas se abren.
3. Latencia total para la segunda: ~5 s (espera + captura propia).

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Cámaras OV7670 sin level shifter dañan el Mega o no responden | Media | Verificar módulo antes de cablear. Comprar TXS0108E si hace falta. |
| Tasa de OCR baja con QQVGA (poca resolución para caracteres) | Alta | Subir a QVGA (acepta latencia +3 s) o agregar zoom óptico (lente macro). |
| FIFO write enable timing finicky → frames corruptos | Alta | Estudiar datasheet del módulo específico, buscar examples de OV7670+AL422 conocidos. |
| Tesseract muy permisivo con caracteres → placas con errores ABC1Z3 vs ABC123 | Media | Whitelist + regex + diccionario de placas conocidas. |
| Concurrencia con dos cámaras → frames corruptos por cruce de OE | Media | Semáforo serial sobre todo el pipeline de captura. |
| Cableado de 26 pines complicado → contactos sueltos | Alta | Usar PCB perforada o protoboard grande con cables cortos del mismo color por función. |

---

## Estimación de esfuerzo

| Fase | Tiempo estimado | Dependencia hardware |
|---|---|---|
| Fase 1 (sketch + 1 cám + Python decoder) | 4-8 h | Cableado físico de CAM1 |
| Fase 2 (bridge binario + dump PNG) | 3-5 h | Ninguna nueva |
| Fase 3 (Tesseract + OCR pipeline) | 6-10 h | Tessdata descargada |
| Fase 4 (CAM2 + concurrencia) | 3-5 h | Cableado físico de CAM2 |
| **Total** | **16-28 h** | — |

---

## Decisiones para confirmar antes de Fase 1

1. **Resolución inicial**: QQVGA (160×120) o QVGA (320×240)?
2. **Move LEDs ahora o en una iteración aparte**? Necesitamos los pines 11/12/13 libres para el XCLK; conviene moverlos en el mismo PR.
3. **OpenCvSharp**: ¿está OK que añadamos esta dependencia gorda (~50 MB de DLLs nativas)?
4. **Tesseract `tessdata`**: ¿bundleamos sólo `eng.traineddata` o queremos soporte multi-idioma?
5. **Fallback de placa**: si OCR falla, ¿usar `UNKNOWN-{ts}` y continuar el flujo, o denegar entrada?

---

## Salida del Plan

Cuando aprobemos este plan, abrimos un branch nuevo (`feature/cameras-lpr`), implementamos fase por fase con commits separados (uno por fase), y al final mergeamos a main. Cada fase debe pasar su criterio de éxito antes de avanzar a la siguiente.
