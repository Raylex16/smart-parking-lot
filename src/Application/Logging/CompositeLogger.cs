using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Application.Logging;

public class CompositeLogger : ILogger
{
    private readonly IReadOnlyList<ILogger> _loggers;

    public CompositeLogger(params ILogger[] loggers)
    {
        _loggers = loggers;
    }

    public void Log(LogLevel level, string source, string message)
    {
        
        foreach (var logger in _loggers)
        {
            logger.Log(level, source, message);
        }
    }
}
