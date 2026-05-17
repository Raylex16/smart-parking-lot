// Delegado al HardwareConfig unificado en Application.Hardware.
// Ambos tipos son intercambiables; los alias de tipo evitan cambios en el código existente.
global using HardwareConfig  = SmartParkingLot.Application.Hardware.HardwareConfig;
global using SensorMapping   = SmartParkingLot.Application.Hardware.SensorMapping;
global using GateMapping     = SmartParkingLot.Application.Hardware.GateMapping;
