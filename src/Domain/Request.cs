using SmartParkingLot.Controllers;
using System;

namespace SmartParkingLot.Domain
{
    public abstract class Request
    {
        public string GateId { get; init; } = "G-01";
        public string VehiclePlate { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.Now;

        public abstract void Execute(GateController gc);
    }
}
