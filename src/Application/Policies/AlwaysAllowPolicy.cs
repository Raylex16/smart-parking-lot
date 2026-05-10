using SmartParkingLot.Core;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Policies;


public class AlwaysAllowPolicy : IAccessPolicy
{
    public bool CanEnter(EntryRequest request) => true;
}
