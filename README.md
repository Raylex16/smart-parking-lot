# Smart Parking Lot

Simulador de ingreso de vehiculos a un parqueadero inteligente, desarrollado en **C# 14 / .NET 10**. El proyecto tiene un enfoque **educativo y de diseno de software**, aplicando intensivamente los patrones y principios **GRASP** (General Responsibility Assignment Software Patterns).

## Objetivo

Demostrar como los principios GRASP guian la asignacion de responsabilidades en un sistema orientado a objetos real, utilizando como caso de uso el control de acceso vehicular a un estacionamiento.

## Principios GRASP Aplicados


| Principio              | Donde se aplica                                                                             |
| ---------------------- | ------------------------------------------------------------------------------------------- |
| **Information Expert** | `ParkingLot` gestiona sus espacios; `ParkingSpot` conoce su propio estado                   |
| **Controller**         | `GateController` recibe el evento de sistema (solicitud de entrada) y orquesta la respuesta |
| **Low Coupling**       | Los controladores dependen de interfaces (`ICapacityService`), no de clases concretas       |
| **High Cohesion**      | Cada clase tiene una unica responsabilidad bien definida                                    |
| **Creator**            | `Program.cs` como Composition Root ensambla todas las dependencias                          |

## Arquitectura

```
src/
├── Program.cs              # Composition Root (DI manual, top-level statements)
├── Controllers/
│   ├── IGateController.cs   # Contrato del controlador de puerta
│   └── GateController.cs    # Controller GRASP: orquesta el caso de uso
├── Services/
│   ├── ICapacityService.cs  # Contrato del servicio de capacidad
│   └── CapacityService.cs   # Coordina consultas de disponibilidad
└── Domain/
    ├── EntryRequest.cs      # Evento de sistema: solicitud de entrada
    ├── ParkingLot.cs        # Information Expert: coleccion de espacios
    └── ParkingSpot.cs       # Information Expert: estado del espacio individual
```

### Flujo del caso de uso

```
EntryRequest ──> GateController ──> ICapacityService ──> ParkingLot ──> ParkingSpot
                  (Controller)       (Low Coupling)    (Info Expert)   (Info Expert)
```

1. Se crea un `EntryRequest` con el ID y tipo del vehiculo.
2. `GateController` recibe la solicitud y consulta a `ICapacityService`.
3. `CapacityService` delega en `ParkingLot` para verificar disponibilidad.
4. `ParkingLot` localiza el primer `ParkingSpot` libre.
5. `ParkingSpot` se marca como ocupado (`Occupy()`).
6. `GateController` abre la puerta si el acceso fue concedido.

## Modelado UML

El diagrama de clases completo del modulo IoT core se encuentra en: 

![Diagrama UML del sistema](docs/uml/uml_2.drawio.png)

[Visualiza en draw.io](https://drive.google.com/file/d/1jrm7Cnc-E39-5w8stSZKdFhLRO3f-935/view?usp=sharing)

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Ejecucion

```bash
# Compilar
dotnet build

# Ejecutar simulador
dotnet run --project src/smart-parking-lot.csproj
```

### Salida esperada

El simulador procesa 4 solicitudes de entrada sobre un parqueadero con 3 espacios:

- Los primeros 3 vehiculos obtienen acceso (espacios A1, A2, B1).
- El 4to vehiculo es rechazado por falta de disponibilidad.

## Tecnologias

- C# 14 con sintaxis moderna (Records, Pattern Matching, top-level statements)
- .NET 10
- Nullable reference types habilitados
