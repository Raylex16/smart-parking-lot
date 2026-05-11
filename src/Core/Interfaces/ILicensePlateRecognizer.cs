namespace SmartParkingLot.Core.Interfaces;

public interface ILicensePlateRecognizer
{
    string Recognize(string gateId);
}
