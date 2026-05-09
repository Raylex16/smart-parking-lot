using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Logging;

public class FileLogger : ILogger
{
    private readonly string _directory;
    private readonly object _writeLock = new();

    public LogLevel MinimumLevel { get; set; }

    public FileLogger(string directory, LogLevel minimumLevel = LogLevel.Debug)
    {
        _directory = directory;
        MinimumLevel = minimumLevel;
        Directory.CreateDirectory(_directory);
    }

    public string GetCurrentLogFilePath() =>
        Path.Combine(_directory, $"parking-{DateTime.Now:yyyy-MM-dd}.log");

    public IEnumerable<string> ListLogFiles() =>
        Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "parking-*.log")
                .OrderByDescending(p => p)
            : [];

    public void Log(LogLevel level, string source, string message)
    {
        if (level < MinimumLevel) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{Tag(level)}][{source}] {message}{Environment.NewLine}";
        lock (_writeLock)
        {
            File.AppendAllText(GetCurrentLogFilePath(), line);
        }
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
