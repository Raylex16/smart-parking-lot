namespace SmartParkingLot.Core.Interfaces;

public interface IAccessPolicy
{
    Task<bool> CanEnterAsync(EntryRequest request);
}
