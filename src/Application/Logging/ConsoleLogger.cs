using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Logging;

public class ConsoleLogger : ILogger
{
    public LogLevel MinimumLevel { get; set; }

    public ConsoleLogger(LogLevel minimumLevel = LogLevel.Info)
    {
        MinimumLevel = minimumLevel;
    }

    public void Log(LogLevel level, string source, string message)
    {
        if (level < MinimumLevel) return;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}][{Tag(level)}][{source}] {message}");
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "LOG"
    };
}
