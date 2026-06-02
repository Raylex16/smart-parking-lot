# Hardware Wiring — Smart Parking Lot

## LCD I2C 16×2 (PCF8574, dirección 0x27)

Conectado al bus `Wire` del Arduino Mega (I2C0, pines 20/21).

```
Módulo I2C LCD       Arduino Mega 2560
  VCC    ──────────→  5V
  GND    ──────────→  GND
  SDA    ──────────→  Pin 20  (SDA / Wire)
  SCL    ──────────→  Pin 21  (SCL / Wire)
```

**Notas:**
- Resistencias pull-up de 4.7 kΩ en SDA y SCL hacia 5V si hay otros dispositivos en el mismo bus.
- No requiere cambios de software; ya funciona con `CMD:ACT:LCD:STATUS` y `CMD:ACT:LCD:MSG`.

---

## Cámara OV7670 (una por puerta)

El OV7670 usa dos interfaces:
- **SCCB** (configuración, compatible I2C): conectado a `Wire1` (I2C1) para no colisionar con el LCD.
- **Bus de datos paralelo 8 bits** (píxeles): pines 22–29 (Port A del Mega).

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
  VSYNC ──────────→  Pin 2   (PE4, INT0)
  HREF  ──────────→  Pin 3   (PE5, INT1)
  PCLK  ──────────→  Pin 4   (PG5)
  XCLK  ──────────→  Pin 11  (PB5 / OC1A, PWM 8 MHz)
  RESET ──────────→  Pin 32  (activo bajo)
  PWDN  ──────────→  Pin 33  (activo alto = apagado; mantener LOW)
```

### Diagrama de buses I2C

```
Arduino Mega
    20 (SDA) ──┬── 4.7kΩ ── 5V
               └── LCD I2C (0x27)

    21 (SCL) ──┬── 4.7kΩ ── 5V
               └── LCD I2C (0x27)

    70 (SDA1) ──── OV7670 SDA  (Wire1, pull-ups internos)
    71 (SCL1) ──── OV7670 SCL  (Wire1)
```

### Adaptación de niveles lógicos

El OV7670 opera a **3.3 V lógico**; los pines del Mega son 5 V. Usar un divisor resistivo (1 kΩ / 2 kΩ) o un nivel-shifter de 4 canales en cada línea D0–D7, VSYNC, HREF y PCLK.

### XCLK

Pin 11 (OC1A) configurado en modo CTC a 8 MHz (Timer1, sin prescaler, OCR1A = 0). Rango aceptable del sensor: 8–24 MHz.

### Dirección SCCB

`0x21` en formato I2C de 7 bits (equivale a 0x42 write / 0x43 read del datasheet OV7670).

### Resolución

QQVGA (160 × 120) en escala de grises (canal Y de YUV422) = 19 200 bytes por frame.

---

## Protocolo serial de captura

Baudrate: **115 200 baud** (cambiado de 9600 para transferencias de imagen en ~2.2 s).

| Dirección    | Formato                           | Descripción                          |
|--------------|-----------------------------------|--------------------------------------|
| C# → Arduino | `CMD:CAM:CAPTURE:{gateId}`        | Solicita captura para la puerta      |
| Arduino → C# | `CAM:BEGIN:{byteCount}:{gateId}`  | Inicia transferencia                 |
| Arduino → C# | `CAM:DATA:{base64Chunk}`          | Chunk base64 (~60 chars/línea)        |
| Arduino → C# | `CAM:END`                         | Frame completo disponible            |
| Arduino → C# | `CAM:ERROR:{reason}`              | Fallo en captura                     |
