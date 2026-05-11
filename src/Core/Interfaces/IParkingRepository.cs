using SmartParkingLot.Core;

namespace SmartParkingLot.Core.Interfaces;

/// <summary>
/// ⚠️ DEPRECATED: Usar interfaces segregadas en su lugar
/// - IParkingLotRepository
/// - ISpotRepository
/// - IRequestRepository
/// - ISensorRepository
/// - IDeviceActionRepository
/// - IAlertRepository
/// </summary>
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
