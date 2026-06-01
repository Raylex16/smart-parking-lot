# Cableado de Hardware — Smart Parking Lot

Guía completa de conexión física para todos los componentes del sistema:
sensores IR de spots, sensores IR de puertas, LEDs, servos, pantalla LCD I2C y cámaras OV7670.

---

## Resumen del sistema

```
                    ┌──────────────────────────────────────────┐
                    │           PC  (C# .NET 10)               │
                    │  ArduinoSerialBridge  LcdDisplay          │
                    └────────┬─────────────────────────────────┘
                             │ USB Serial (Serial0, 115200 baud)
                             │
              ┌──────────────▼──────────────────────────────────┐
              │           Arduino Mega 2560  (Master)            │
              │  spot_sensor.ino                                  │
              │                                                   │
              │  • 4 sensores IR de spot  (pines 7, 6, 5, 9)     │
              │  • 4 LEDs de spot         (pines 13, 12, 11, 10) │
              │  • 2 sensores IR de puerta (pines 4, 3)          │
              │  • 2 cámaras OV7670       (Wire + Port A)        │
              │    G-01 PWDN→pin33  G-02 PWDN→pin34              │
              │    uso disyuntivo: solo una activa a la vez       │
              │  • LCD I2C 16×2           (Wire  / 0x27)         │
              │                                                   │
              │  I2C Wire (pines 20 SDA / 21 SCL) — único bus    │
              │    ├─── LCD 16×2         dirección 0x27          │
              │    ├─── Uno Esclavo      dirección 0x08          │
              │    └─── OV7670 G-01/G-02 dirección 0x21          │
              │         (sin conflicto — direcciones distintas)  │
              └──────────────┬──────────────────────────────────┘
                             │ I2C (SDA→A4 / SCL→A5)
              ┌──────────────▼──────────────────────────────────┐
              │        Arduino Uno  (Esclavo de puertas)         │
              │  gate_slave.ino                                   │
              │  • Servo puerta entrada  (pin 9)                  │
              │  • Servo puerta salida   (pin 10)                 │
              └─────────────────────────────────────────────────┘
```

---

## 1. Sensores IR de spot (FC-51) + LEDs

**Arduino Mega** — sketch `arduino/spot_sensor/spot_sensor.ino`

| Componente     | Pin Mega | Spot | Activo |
|----------------|----------|------|--------|
| Sensor IR1 OUT | **D7**   | A-1  | LOW    |
| Sensor IR2 OUT | **D6**   | A-2  | LOW    |
| Sensor IR3 OUT | **D5**   | A-3  | LOW    |
| Sensor IR4 OUT | **D9**   | A-4  | LOW    |
| LED1           | **D13**  | A-1  | HIGH   |
| LED2           | **D12**  | A-2  | HIGH   |
| LED3           | **D11**  | A-3  | HIGH   |
| LED4           | **D10**  | A-4  | HIGH   |

```
                     Arduino Mega 2560
                    ┌─────────────────┐
             5V ────┤ 5V              │
            GND ────┤ GND             │
                    │                 │
  [IR FC-51 A-1]    │                 │
   VCC ─── 5V       │                 │
   GND ─── GND      │                 │
   OUT ─────────────┤ D7              │
                    │                 │
  [IR FC-51 A-2]    │                 │
   OUT ─────────────┤ D6              │
                    │                 │
  [IR FC-51 A-3]    │                 │
   OUT ─────────────┤ D5              │
                    │                 │
  [IR FC-51 A-4]    │                 │
   OUT ─────────────┤ D9              │
                    │                 │
  [LED1] ──R220Ω────┤ D13             │
  [LED2] ──R220Ω────┤ D12             │
  [LED3] ──R220Ω────┤ D11             │
  [LED4] ──R220Ω────┤ D10             │
                    └─────────────────┘
```

> **Sensor FC-51:** `LOW` = vehículo detectado (ocupado) · `HIGH` = libre.  
> El sketch invierte la señal y emite `EVT:SENSOR:IR<n>:0` o `EVT:SENSOR:IR<n>:1`.

---

## 2. Sensores IR de puerta (detección de vehículo en acceso)

| Componente       | Pin Mega | Puerta          |
|------------------|----------|-----------------|
| Sensor IR puerta 1 OUT | **D4** | G-01 (entrada) |
| Sensor IR puerta 2 OUT | **D3** | G-02 (salida)  |

```
  [IR FC-51 Puerta G-01]           Arduino Mega
   VCC ─── 5V                    ┌─────────────┐
   GND ─── GND                   │             │
   OUT ────────────────────────→ │ D4          │
                                  │             │
  [IR FC-51 Puerta G-02]          │             │
   OUT ────────────────────────→ │ D3          │
                                  └─────────────┘
```

> Emite `EVT:SENSOR:GATE-IR1:1` / `EVT:SENSOR:GATE-IR2:1` cuando detecta vehículo.

---

## 3. Pantalla LCD I2C 16×2

Módulo LCD con backpack **PCF8574** (dirección I2C `0x27`).  
Se conecta al bus **Wire** del Mega (I2C0, compartido con el esclavo de puertas).

| Pin LCD    | Pin Mega      |
|------------|---------------|
| VCC        | 5V            |
| GND        | GND           |
| SDA        | **Pin 20** (SDA / Wire) |
| SCL        | **Pin 21** (SCL / Wire) |

```
  Módulo I2C LCD            Arduino Mega 2560
  ┌──────────┐             ┌──────────────────┐
  │  VCC ────┼─────────────┤ 5V               │
  │  GND ────┼─────────────┤ GND              │
  │  SDA ────┼──────┬──────┤ 20 (SDA / Wire)  │
  │  SCL ────┼──┬───┼──────┤ 21 (SCL / Wire)  │
  └──────────┘  │   │      └──────────────────┘
                │   │
         4.7kΩ  │   │ 4.7kΩ
            ────┤   ├────
               5V   5V       ← resistencias pull-up (necesarias si
                                no las trae el módulo backpack)
```

> Comandos enviados por C#:  
> `CMD:ACT:LCD:STATUS:{disponibles}:{total}` → actualiza el display  
> `CMD:ACT:LCD:MSG:{texto}` → muestra mensaje temporal (3 s)

---

## 4. Esclavo de puertas — Arduino Uno (servos)

Sketch `arduino/gate_slave/gate_slave.ino`. Controla ambos servos de puerta recibiendo comandos por I2C desde el Mega (dirección esclavo `0x08`).

| Conexión I2C     | Pin Uno | Pin Mega |
|------------------|---------|----------|
| SDA              | A4      | 20       |
| SCL              | A5      | 21       |
| GND común        | GND     | GND      |

| Servo            | Pin Uno | Puerta            |
|------------------|---------|-------------------|
| Servo GATE1      | **D9**  | G-01 (entrada)    |
| Servo GATE2      | **D10** | G-02 (salida)     |

```
  Arduino Mega                  Arduino Uno (Esclavo 0x08)
  ┌──────────┐                  ┌────────────────────────┐
  │ Pin 20 ──┼──── SDA ─────────┼─ A4 (SDA)              │
  │ Pin 21 ──┼──── SCL ─────────┼─ A5 (SCL)              │
  │ GND    ──┼──── GND ─────────┼─ GND                   │
  └──────────┘                  │                        │
                                │ D9  ──── Señal Servo G-01 (entrada)
                                │ D10 ──── Señal Servo G-02 (salida)
                                │                        │
                                │ VIN / 5V ← Fuente ext. │  ← ⚠ los servos
                                │ GND      ← Fuente ext. │    consumen mucho;
                                └────────────────────────┘    no usar el 5V del Arduino
```

> **⚠ Alimentación de servos:** los servos SG90/MG996 consumen picos de corriente que exceden el regulador del Uno. Usar una fuente de 5V/2A externa; conectar GND de la fuente al GND del Uno y del Mega para referencia común.

---

## 5. Cámaras OV7670 — G-01 y G-02 (bus compartido, uso disyuntivo)

Ambas cámaras comparten **todos** los pines del Mega. El pin **PWDN** (activo HIGH = apagado) de cada cámara se controla individualmente desde el Mega para seleccionar cuál está activa. Cuando PWDN=HIGH, todos los pines de salida del OV7670 (D0–D7, VSYNC, HREF, PCLK) quedan en alta impedancia y no interfieren con el bus.

> **Restricción de software:** nunca activar las dos cámaras simultáneamente (ambas PWDN=LOW). Ambas tienen dirección SCCB fija `0x21` — con las dos activas colisionarían en el bus.

> **⚠ El OV7670 opera a 3.3 V lógico.** El XCLK sale del Mega a 5V hacia la cámara — conectar directamente es fuera de spec pero funciona en práctica. Las salidas de la cámara (D0–D7, VSYNC, HREF, PCLK) a 3.3V son legibles por el Mega sin level shifter ya que 3.3V supera el umbral HIGH (~2V).

### Pines SCCB

El Mega 2560 tiene **un solo bus I2C hardware**: Wire en pines 20 (SDA) y 21 (SCL). Todos los dispositivos I2C van ahí — las direcciones no colisionan (`0x21`, `0x27`, `0x08`).

> La SCCB del OV7670 tolera el bus a 5V en la práctica. Los pull-ups del backpack LCD (a 5V) son suficientes para toda la línea — no se necesitan pull-ups adicionales para la cámara.

### Tabla de pines — compartidos G-01 y G-02

| Pin OV7670   | Pin Mega | Descripción                                      |
|--------------|----------|--------------------------------------------------|
| VCC / 3.3V   | 3.3V     | Alimentación ⚠ NO 5V — ambas cámaras al mismo nodo |
| GND          | GND      | Tierra común                                     |
| SDA (SCCB)   | **20**   | Wire (compartido con LCD 0x27 y Uno 0x08)        |
| SCL (SCCB)   | **21**   | Wire (compartido con LCD 0x27 y Uno 0x08)        |
| D0           | **22**   | PA0 — compartido, alta impedancia cuando PWDN=HIGH |
| D1           | **23**   | PA1                                              |
| D2           | **24**   | PA2                                              |
| D3           | **25**   | PA3                                              |
| D4           | **26**   | PA4                                              |
| D5           | **27**   | PA5                                              |
| D6           | **28**   | PA6                                              |
| D7           | **29**   | PA7                                              |
| VSYNC        | **2**    | INT0 — compartido                                |
| HREF         | **30**   | Compartido                                       |
| PCLK / MCLK  | **31**   | Compartido                                       |
| XCLK / MCLK  | **8**    | OC4C (Timer4) ~4 MHz — compartido               |
| RESET        | **32**   | Compartido — HIGH para operar                    |

### Pines individuales por cámara

| Señal | Pin Mega | Cámara | Lógica |
|-------|---------|--------|--------|
| PWDN  | **33**  | G-01   | HIGH = apagada · LOW = activa |
| PWDN  | **34**  | G-02   | HIGH = apagada · LOW = activa |

### Pull-ups SCCB

Los pull-ups del backpack LCD (4.7 kΩ a 5V, ya incorporados en el módulo) cubren toda la línea Wire. No se necesitan resistencias adicionales para la cámara.

### Diagrama de conexión

```
  OV7670 G-01        OV7670 G-02             Arduino Mega 2560
  ┌──────────┐       ┌──────────┐           ┌───────────────────────┐
  │ VCC ─────┼───┬───┼── VCC    │           │ 3.3V                  │
  │ GND ─────┼───┴───┼── GND ───┼───────────┤ GND                   │
  │ SDA ─────┼───┬───┼── SDA    │           │                       │
  │ SCL ─────┼───┴───┼── SCL ───┼───────────┤ Pin 20 / 21  (Wire)   │
  │          │       │          │           │                       │
  │ D0 ──────┼───┬───┼── D0     │           │                       │
  │ D1 ──────┼───┤   │  D1 ─────┼───────────┤ 22–29    (Port A)     │
  │  ...     │   │   │   ...    │           │                       │
  │ D7 ──────┼───┘   └── D7 ────┘           │                       │
  │ VSYNC ───┼───────────VSYNC ─────────────┤ Pin 2    (INT0)       │
  │ HREF  ───┼───────────HREF  ─────────────┤ Pin 30               │
  │ PCLK  ───┼───────────PCLK  ─────────────┤ Pin 31               │
  │ XCLK  ───┼───────────XCLK  ─────────────┤ Pin 8    (OC4C)       │
  │ RESET ───┼───────────RESET ─────────────┤ Pin 32               │
  │ PWDN  ───┼───────────────────────────────┤ Pin 33  (G-01)       │
  │          │       PWDN ──────────────────┤ Pin 34  (G-02)       │
  └──────────┘       └──────────┘           └───────────────────────┘
```

### Selección de cámara por software

```cpp
#define PIN_PWDN_G01 33
#define PIN_PWDN_G02 34

void selectCamera(uint8_t gateId) {
    if (gateId == 1) {
        digitalWrite(PIN_PWDN_G02, HIGH);  // apagar G-02
        delay(10);
        digitalWrite(PIN_PWDN_G01, LOW);   // encender G-01
    } else {
        digitalWrite(PIN_PWDN_G01, HIGH);  // apagar G-01
        delay(10);
        digitalWrite(PIN_PWDN_G02, LOW);   // encender G-02
    }
    delay(100);        // estabilización del sensor
    configureCamera(); // reconfigurar — PWDN borra los registros
}
```

> La cámara pierde su configuración al salir de PWDN. `configureCamera()` agrega ~300 ms al cambio, aceptable para un parqueadero.

### Clock XCLK (pin 8 — Timer4C)

Timer4 configurado en **Fast PWM Modo 14** (WGM=1110), TOP=ICR4, sin prescaler:

```cpp
TCCR4A = (1 << COM4C1) | (1 << WGM41);
TCCR4B = (1 << WGM43)  | (1 << WGM42) | (1 << CS40);
ICR4   = 3;   // F = 16 MHz / (ICR4+1) = 4 MHz, 50% duty con OCR4C=1
OCR4C  = 1;
```

`F_XCLK = 16 MHz / (3+1) = **4 MHz**` — compartido por ambas cámaras simultáneamente (el XCLK siempre corre; no afecta a la cámara apagada).

---

## 6. Mapa completo de pines — Arduino Mega (Master)

| Pin   | Función                                    | Componente                    |
|-------|--------------------------------------------|-------------------------------|
| 0     | RX0 (Serial USB)                           | PC ↔ C#                       |
| 1     | TX0 (Serial USB)                           | PC ↔ C#                       |
| 2     | INT0 — VSYNC cámaras G-01/G-02 (compartido)| OV7670 G-01 y G-02            |
| 3     | Entrada digital — IR puerta G-02           | Sensor IR FC-51               |
| 4     | Entrada digital — IR puerta G-01           | Sensor IR FC-51               |
| 5     | Entrada digital — IR spot A-3              | Sensor IR FC-51               |
| 6     | Entrada digital — IR spot A-2              | Sensor IR FC-51               |
| 7     | Entrada digital — IR spot A-1              | Sensor IR FC-51               |
| 8     | OC4C — XCLK cámaras G-01/G-02 (4 MHz)     | OV7670 G-01 y G-02            |
| 9     | Entrada digital — IR spot A-4              | Sensor IR FC-51               |
| 10    | Salida PWM — LED spot A-4                  | LED + R220Ω                   |
| 11    | Salida PWM — LED spot A-3                  | LED + R220Ω                   |
| 12    | Salida PWM — LED spot A-2                  | LED + R220Ω                   |
| 13    | Salida PWM — LED spot A-1                  | LED + R220Ω                   |
| 14–19 | LIBRES                                     | —                             |
| 20    | SDA (Wire) — único bus I2C                 | LCD 0x27 + Uno 0x08 + OV7670 0x21 |
| 21    | SCL (Wire) — único bus I2C                 | LCD 0x27 + Uno 0x08 + OV7670 0x21 |
| 22    | PA0 — D0 datos cámaras (compartido)        | OV7670 G-01 y G-02            |
| 23    | PA1 — D1                                   | OV7670 G-01 y G-02            |
| 24    | PA2 — D2                                   | OV7670 G-01 y G-02            |
| 25    | PA3 — D3                                   | OV7670 G-01 y G-02            |
| 26    | PA4 — D4                                   | OV7670 G-01 y G-02            |
| 27    | PA5 — D5                                   | OV7670 G-01 y G-02            |
| 28    | PA6 — D6                                   | OV7670 G-01 y G-02            |
| 29    | PA7 — D7                                   | OV7670 G-01 y G-02            |
| 30    | HREF cámaras (compartido)                  | OV7670 G-01 y G-02            |
| 31    | PCLK cámaras (compartido)                  | OV7670 G-01 y G-02            |
| 32    | RESET cámaras (compartido)                 | OV7670 G-01 y G-02            |
| 33    | PWDN cámara G-01                           | OV7670 G-01                   |
| 34    | PWDN cámara G-02                           | OV7670 G-02                   |
| 35–69 | LIBRES                                     | —                             |
| 70–71 | No existen en el Mega 2560                 | —                             |

---

## 7. Mapa completo de pines — Arduino Uno (Esclavo de puertas)

| Pin  | Función                          |
|------|----------------------------------|
| A4   | SDA (Wire) ← Pin 20 del Mega     |
| A5   | SCL (Wire) ← Pin 21 del Mega     |
| 9    | Señal servo GATE1 (entrada)      |
| 10   | Señal servo GATE2 (salida)       |
| GND  | GND común con Mega y fuente ext. |
| VIN  | 5V fuente externa (servos)       |

---

## 9. Alimentación — diagrama general

```
  [Fuente 12V/2A] ─── [Regulador 5V/2A] ─┬─ Arduino Mega  (VIN o 5V)
                                           ├─ Arduino Uno   (VIN o 5V)
                                           ├─ Servos        (GND + 5V directo)
                                           └─ Sensores IR   (GND + 5V)

  [Mega 3.3V] ─────┬──── OV7670 G-01 VCC
                   └──── OV7670 G-02 VCC
                         (ambas al mismo nodo 3.3V del Mega)

  GND común: todos los GND conectados entre sí
             (Mega, Uno, fuente externa, ambas cámaras)
```

> **⚠ No alimentar los servos desde el pin 5V del Arduino.** Los picos de corriente de un servo SG90 superan los 500 mA y pueden reiniciar el microcontrolador.

---

## 10. Protocolo serial — referencia rápida

| Dirección    | Formato                            | Ejemplo                      |
|--------------|------------------------------------|------------------------------|
| Arduino → C# | `EVT:SENSOR:IR<n>:<0\|1>`          | `EVT:SENSOR:IR2:1`           |
| Arduino → C# | `EVT:SENSOR:GATE-IR<n>:<0\|1>`     | `EVT:SENSOR:GATE-IR1:1`      |
| C# → Arduino | `CMD:ACT:LED<n>:SET:<0\|1>`        | `CMD:ACT:LED2:SET:1`         |
| C# → Arduino | `CMD:ACT:GATE<n>:ANGLE:<grados>`   | `CMD:ACT:GATE1:ANGLE:90`     |
| C# → Arduino | `CMD:ACT:LCD:STATUS:<lib>:<total>` | `CMD:ACT:LCD:STATUS:3:5`     |
| C# → Arduino | `CMD:ACT:LCD:MSG:<texto>`          | `CMD:ACT:LCD:MSG:BIENVENIDO` |
| C# → Arduino | `CMD:CAM:CAPTURE:<gateId>`         | `CMD:CAM:CAPTURE:G-01`       |
| Arduino → C# | `CAM:BEGIN:<bytes>:<gateId>`       | `CAM:BEGIN:19200:G-01`       |
| Arduino → C# | `CAM:DATA:<base64chunk>`           | `CAM:DATA:SGVsbG8...`        |
| Arduino → C# | `CAM:END`                          | `CAM:END`                    |
| Arduino → C# | `ACK:<actuatorId>`                 | `ACK:LED2`                   |
| Arduino → C# | `NACK:<actuatorId>:<razón>`        | `NACK:LED2:unsupported`      |
| Mega ↔ Slave | `GATE<n>:ANGLE:<grados>\n` (I2C)   | `GATE1:ANGLE:90`             |
