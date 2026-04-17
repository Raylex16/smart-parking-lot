# Arduino Serial Bridge - Spec de Diseno

## Resumen

Establecer comunicacion serial entre C# (.NET 10) y Arduino para leer sensores IR fisicos y alimentar la capa de aplicacion del Smart Parking Lot. Reemplaza la simulacion de sensores de spot con lecturas reales de hardware.

## Decisiones de Diseno

| Decision | Eleccion | Razon |
|----------|----------|-------|
| Enfoque de integracion | Bridge alimenta sensores via `ISensorCapture<T>` | Reutiliza arquitectura GRASP actual, respeta DIP |
| Protocolo serial | `SENSOR_ID:VALOR\n` (ej. `IR1:1`) | Auto-descriptivo, escala sin modificar parser |
| Mapeo de IDs | Hardware ID -> Spot ID en C# (no en Arduino) | Desacoplamiento: Arduino no conoce el dominio |
| Asignacion de spot IDs | Dinamica via diccionario en Composition Root | Flexible, reasignable sin re-flashear Arduino |

## Protocolo Serial

### Formato

Cada linea enviada por el Arduino sigue el formato:

```
SENSOR_ID:VALOR\n
```

- `SENSOR_ID`: Identificador de hardware del sensor (ej. `IR1`, `IR2`, `IR3`)
- `VALOR`: `1` (ocupado / objeto detectado) o `0` (libre / sin deteccion)
- Separador: caracter `:`
- Terminador: `\n` (newline, enviado por `Serial.println()`)

### Ejemplo de flujo serial

```
IR1:1
IR2:0
IR3:1
IR1:1
IR2:1
IR3:1
```

El Arduino itera todos sus sensores en cada ciclo del `loop()`, enviando una linea por sensor con un delay entre ciclos completos.

### Parametros de conexion

- Baud rate: 9600
- Puerto: configurable (ej. `COM6`)
- Driver: no oficial (CH340/CH341)

## Sketch Arduino (modificado)

### Cambios respecto al sketch actual

1. Soportar multiples sensores IR con IDs de hardware
2. Enviar formato `SENSOR_ID:VALOR` en lugar de strings `"Ocupado"`/`"Libre"`
3. Mantener LED pin 13 como indicador (encendido si cualquier sensor detecta)

### Estructura del sketch

- Array de pines IR con sus IDs asociados
- Loop itera cada sensor, lee `digitalRead()`, envia `ID:valor`
- Delay de 2 segundos entre ciclos completos

## Principios SOLID

### ISensorCapture\<T\> — Interface Segregation + Dependency Inversion

La interfaz `ISensor` existente expone metodos de lectura (`ReadValue()`, `GetSensorType()`), pero el bridge necesita **escribir** lecturas, no leerlas. Agregar `CaptureReading()` a `ISensor` violaria ISP (los consumidores que solo leen se verian forzados a conocer un metodo de escritura).

Se crea una interfaz segregada en Core:

```csharp
// Core/Ports/ISensorCapture.cs
public interface ISensorCapture<T> where T : SensorReading
{
    T CaptureReading(T reading);
}
```

- **ISP**: Separa la capacidad de capturar lecturas de la capacidad de leerlas. Cada consumidor depende solo de lo que necesita.
- **DIP**: `ArduinoSerialBridge` depende de la abstraccion `ISensorCapture<SpotSensorReading>`, no del concreto `Sensor<T>`.
- **SRP**: El bridge solo comunica serial. El sensor solo almacena lecturas. La interfaz solo define el contrato de captura.
- **OCP**: Nuevos tipos de sensores (gate, ultrasonido) implementan `ISensorCapture<T>` sin modificar el bridge.
- **LSP**: Cualquier implementacion de `ISensorCapture<SpotSensorReading>` es intercambiable en el bridge.

`Sensor<T>` implementara `ISensorCapture<T>` ademas de `ISensor`:

```csharp
public class Sensor<T> : ISensor, ISensorCapture<T> where T : SensorReading
```

## ArduinoSerialBridge (C#)

### Ubicacion

`src/Hardware/ArduinoSerialBridge.cs` — namespace `SmartParkingLot.Hardware`

### Patron GRASP

**Pure Fabrication**: No existe en el dominio del parqueadero. Es una abstraccion de infraestructura que traduce comunicacion serial a lecturas de dominio.

### Dependencia NuGet

`System.IO.Ports` en el proyecto `SmartParkingLot.Hardware`

### Constructor

```csharp
ArduinoSerialBridge(string portName, int baudRate, Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)> sensorMap)
```

- `portName`: Puerto serial (ej. `"COM6"`)
- `baudRate`: Velocidad (ej. `9600`)
- `sensorMap`: Mapeo de hardware ID a tupla `(SpotId, Sensor)`. El bridge traduce IDs de hardware (ej. `"IR1"`) a IDs de dominio (ej. `"A1"`) al crear las lecturas. El bridge depende de la interfaz `ISensorCapture`, no del concreto `Sensor<T>` (DIP).

### Metodos publicos

| Metodo | Responsabilidad |
|--------|----------------|
| `StartListening()` | Abre el puerto serial, inicia hilo de fondo para lectura continua |
| `StopListening()` | Cierra el puerto, detiene el hilo |
| `Dispose()` | Implementa `IDisposable`, limpieza del puerto serial |

### Logica de parseo

1. Lee una linea del puerto serial (blocking read en hilo de fondo)
2. Separa por `:` -> obtiene `[sensorId, value]`
3. Valida formato: exactamente 2 partes, valor es `"0"` o `"1"`
4. Busca `sensorId` en `sensorMap`
5. Si existe: crea `SpotSensorReading(spotId, isOccupied)` y llama `ISensorCapture.CaptureReading(reading)`
6. Si no existe o formato invalido: log de advertencia, ignora la linea

### Manejo de errores

- Puerto no disponible: log de error al intentar abrir, no lanza excepcion al caller
- Desconexion durante lectura: log de error, intenta reconectar o se detiene
- Formato invalido: log de advertencia, ignora la linea, continua leyendo

## Integracion en Program.cs (Composition Root)

```csharp
// SOLID - DIP: El diccionario se tipa con la interfaz ISensorCapture, no con Sensor<T> concreto
// Mapeo: hardware ID -> (spot ID del dominio, sensor)
var sensorMap = new Dictionary<string, (string SpotId, ISensorCapture<SpotSensorReading> Sensor)>
{
    ["IR1"] = ("A1", spotSensorA1),
    ["IR2"] = ("A2", spotSensorA2),
    ["IR3"] = ("B1", spotSensorB1)
};

var bridge = new ArduinoSerialBridge("COM6", 9600, sensorMap);
bridge.StartListening();
```

El flujo completo de datos:

```
Arduino (pin IR) -> Serial ("IR1:1\n") -> ArduinoSerialBridge.parsea() -> sensorMap["IR1"] = ("A1", sensor) -> CaptureReading(new SpotSensorReading("A1", true))
```

Para que la lectura llegue a la capa de aplicacion, se usa `CapacityService.UpdateSpotState()` con el reading capturado, como ya se hace en la simulacion actual.

## Verificacion

1. El bridge imprime en consola cada lectura recibida del Arduino con formato `[ArduinoSerialBridge] ...`
2. Se confirma que `Sensor<SpotSensorReading>.GetSnapshot()` refleja la lectura real
3. Se llama `CapacityService.UpdateSpotState()` y se verifica que el estado del spot cambia

## Escalabilidad futura (no implementar ahora)

- **Multiples Arduinos**: Un `ArduinoSerialBridge` por puerto serial, cada uno con su propio `sensorMap`
- **Sensores de gate**: Mapeo generico o bridge separado con `Sensor<GateSensorReading>`
- **Protocolo bidireccional**: C# podria enviar comandos al Arduino (ej. abrir gate via servo)

## Alcance de este commit

Solo se implementa:
- Interfaz `ISensorCapture<T>` en Core/Ports (SOLID - ISP + DIP)
- `Sensor<T>` implementa `ISensorCapture<T>`
- Sketch Arduino modificado para formato `SENSOR_ID:VALOR` con un sensor IR
- Clase `ArduinoSerialBridge` en Hardware layer
- Integracion basica en Program.cs
- Verificacion de que el dato llega a la capa de aplicacion

Commit: `feat: arduino serial bridge + IR sensor sketch`
