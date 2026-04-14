using SmartParkingLot.Domain;

namespace SmartParkingLot.Controllers;

public interface IGateController
{
    bool ProcessEntryRequest(EntryRequest request);
}
