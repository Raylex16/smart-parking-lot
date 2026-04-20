// Program.cs — Composition Root: punto de entrada mínimo, delega el arranque a ParkingLotApp.

var app = new SmartParkingLot.Cli.ParkingLotApp();
await app.RunAsync();
