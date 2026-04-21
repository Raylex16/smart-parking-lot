# Conexión de Hardware — Smart Parking Lot

Guía de conexión física para 3 sensores IR y 3 LEDs al Arduino Uno/Nano.

---

## Componentes necesarios

| Cantidad | Componente              | Descripción                              |
|----------|-------------------------|------------------------------------------|
| 3        | Sensor IR FC-51         | Detecta presencia del vehículo en el spot |
| 3        | LED (rojo o verde)      | Indica estado del spot (ocupado/libre)   |
| 3        | Resistencia 220Ω        | Protección para cada LED                 |
| 1        | Arduino Uno o Nano      | Microcontrolador central                 |
| 1        | Cable USB A–B / Mini-USB| Conexión serial con la PC                |
| —        | Protoboard + cables     | Para armado del circuito                 |

---

## Mapa de pines

| Componente  | Pin Arduino | Spot asociado |
|-------------|-------------|---------------|
| Sensor IR1  | **D7**      | A1            |
| Sensor IR2  | **D6**      | A2            |
| Sensor IR3  | **D5**      | A3            |
| LED1        | **D13**     | A1            |
| LED2        | **D12**     | A2            |
| LED3        | **D11**     | A3            |

---

## Diagrama de conexión

```
                        Arduino Uno / Nano
                       ┌──────────────────┐
              5V ──────┤ 5V               │
             GND ──────┤ GND              │
                       │                  │
   [Sensor IR1]  OUT ──┤ D7   (Spot A1)   │
   [Sensor IR2]  OUT ──┤ D6   (Spot A2)   │
   [Sensor IR3]  OUT ──┤ D5   (Spot A3)   │
                       │                  │
   [LED1] ──R220Ω──────┤ D13  (Spot A1)   │
   [LED2] ──R220Ω──────┤ D12  (Spot A2)   │
   [LED3] ──R220Ω──────┤ D11  (Spot A3)   │
                       │                  │
                       │ USB → PC (Serial)│
                       └──────────────────┘
```

---

## Conexión de cada sensor IR (FC-51)

El sensor FC-51 tiene 3 pines:

```
Sensor IR FC-51
┌─────────┐
│  VCC ───┼──────── 5V  (Arduino)
│  GND ───┼──────── GND (Arduino)
│  OUT ───┼──────── D7 / D6 / D5 (según spot)
└─────────┘
```

> **Nota:** El sensor FC-51 es activo en LOW.  
> `LOW` = objeto detectado = spot **ocupado**  
> `HIGH` = sin objeto = spot **libre**  
> El sketch invierte esta señal antes de enviarla por serial.

---

## Conexión de cada LED

```
Arduino D13/D12/D11
        │
        ├──── R 220Ω ──── Ánodo (+) [LED] Cátodo (–) ──── GND
```

> El cátodo del LED es el pin más corto (–). Siempre conectar la resistencia  
> en serie para no superar los 40 mA del pin digital.

---

## Comportamiento LED ↔ Spot

| Estado del Spot | Señal enviada por C# | LED          |
|-----------------|----------------------|--------------|
| Ocupado         | `CMD:ACT:LED<n>:SET:1` | Encendido  |
| Libre           | `CMD:ACT:LED<n>:SET:0` | Apagado    |

C# envía el comando al Arduino vía serial **automáticamente** después de actualizar el estado del spot en la base de datos.

---

## Protocolo serial (referencia rápida)

| Dirección       | Formato                          | Ejemplo               |
|-----------------|----------------------------------|-----------------------|
| Arduino → C#    | `EVT:SENSOR:IR<n>:<0\|1>`        | `EVT:SENSOR:IR2:1`    |
| C# → Arduino    | `CMD:ACT:LED<n>:SET:<0\|1>`      | `CMD:ACT:LED2:SET:1`  |
| Arduino → C# ✓  | `ACK:<actuatorId>`               | `ACK:LED2`            |
| Arduino → C# ✗  | `NACK:<actuatorId>:<razón>`      | `NACK:LED2:unsupported` |

---

## Agregar un 4.° sensor (escalabilidad)

El sketch está diseñado para escalar. Solo edita `arduino/spot_sensor/spot_sensor.ino`:

```cpp
const int SENSOR_COUNT = 4;                              // 1. Aumentar conteo

const int IR_PINS[SENSOR_COUNT]  = { 7, 6, 5, 4 };      // 2. Agregar pin IR
const int LED_PINS[SENSOR_COUNT] = { 13, 12, 11, 10 };  // 3. Agregar pin LED
```

Y en `ParkingLotApp.cs`, agregar la entrada correspondiente en los diccionarios de mapeo.
