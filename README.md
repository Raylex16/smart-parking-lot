# Smart Parking Lot

A smart parking lot entry simulator built with **C# 14 / .NET 10**. This project has an **educational software design** focus and applies **GRASP** principles and patterns (General Responsibility Assignment Software Patterns).

## Objective

Demonstrate how GRASP principles guide responsibility assignment in a real object-oriented system, using vehicle access control in a parking lot as the use case.

## Applied GRASP Principles


| Principle              | Where it is applied                                                                         |
| ---------------------- | ------------------------------------------------------------------------------------------- |
| **Information Expert** | `ParkingLot` manages its spots; `ParkingSpot` knows its own state                           |
| **Controller**         | `GateController` receives the system event (entry request) and orchestrates the response    |
| **Low Coupling**       | Controllers depend on interfaces (`ICapacityService`), not concrete classes                 |
| **High Cohesion**      | Each class has one clearly defined responsibility                                           |
| **Creator**            | `Program.cs` as Composition Root assembles all dependencies                                 |

## Architecture

```
src/
├── Program.cs              # Composition Root (DI manual, top-level statements)
├── Controllers/
│   ├── IGateController.cs   # Gate controller contract
│   └── GateController.cs    # GRASP Controller: orchestrates the use case
├── Services/
│   ├── ICapacityService.cs  # Capacity service contract
│   └── CapacityService.cs   # Coordinates availability checks
└── Domain/
    ├── EntryRequest.cs      # System event: entry request
    ├── ParkingLot.cs        # Information Expert: spot collection
    └── ParkingSpot.cs       # Information Expert: individual spot state
```

### Use Case Flow

```
EntryRequest ──> GateController ──> ICapacityService ──> ParkingLot ──> ParkingSpot
                   (Controller)       (Low Coupling)    (Info Expert)   (Info Expert)
```

1. An `EntryRequest` is created with the vehicle ID and type.
2. `GateController` receives the request and queries `ICapacityService`.
3. `CapacityService` delegates to `ParkingLot` to verify availability.
4. `ParkingLot` finds the first available `ParkingSpot`.
5. `ParkingSpot` is marked as occupied (`Occupy()`).
6. `GateController` opens the gate when access is granted.

## UML Modeling

The full class diagram of the IoT core module is available here:

![System UML Diagram](docs/uml/uml_2.drawio.png)

[View on draw.io](https://drive.google.com/file/d/1jrm7Cnc-E39-5w8stSZKdFhLRO3f-935/view?usp=sharing)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

```bash
# Build
dotnet build

# Run simulator
dotnet run --project smart-parking-lot.csproj
```

### Expected Output

The simulator processes 4 entry requests for a parking lot with 3 spots:

- The first 3 vehicles are granted access (spots A1, A2, B1).
- The 4th vehicle is rejected due to lack of availability.

## Technologies

- C# 14 with modern sintax (Records, Pattern Matching, top-level statements)
- .NET 10
- Nullable reference types enabled
