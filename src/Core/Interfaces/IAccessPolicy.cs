namespace SmartParkingLot.Core.Interfaces;


public interface IAccessPolicy
{
    bool CanEnter(EntryRequest request);
}
