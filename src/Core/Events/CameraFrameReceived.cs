namespace SmartParkingLot.Core.Events;

public record CameraFrameReceived(string GateId, byte[] ImageBytes);
