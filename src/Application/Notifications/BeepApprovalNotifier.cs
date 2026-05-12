using System.Runtime.Versioning;
using SmartParkingLot.Core.Approvals;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Notifications;

public sealed class BeepApprovalNotifier : IApprovalNotifier
{
    private const int BEEP_FREQUENCY_HZ = 1000;
    private const int BEEP_DURATION_MS = 300;

    public void Notify(PendingApproval approval)
    {
        if (OperatingSystem.IsWindows())
            BeepWindows();
    }

    [SupportedOSPlatform("windows")]
    private static void BeepWindows() => Console.Beep(BEEP_FREQUENCY_HZ, BEEP_DURATION_MS);
}
