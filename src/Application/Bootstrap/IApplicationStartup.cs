namespace SmartParkingLot.Application.Bootstrap;

public interface IApplicationStartup
{
    Task StartAsync(CancellationToken ct = default);
}
