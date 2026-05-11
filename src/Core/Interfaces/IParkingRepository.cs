using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

[Obsolete("Use segregated interfaces instead: IParkingLotRepository, ISpotRepository, IRequestRepository, ISensorRepository, IDeviceActionRepository, IAlertRepository", false)]
public interface IParkingRepository :
    IParkingLotRepository,
    ISpotRepository,
    IRequestRepository,
    ISensorRepository,
    IDeviceActionRepository,
    IAlertRepository
{
}
