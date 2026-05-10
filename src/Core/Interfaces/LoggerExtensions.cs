namespace SmartParkingLot.Core.Interfaces;

public static class LoggerExtensions
{
    public static void Debug(this ILogger logger, string source, string message)
        => logger.Log(LogLevel.Debug, source, message);

    public static void Info(this ILogger logger, string source, string message)
        => logger.Log(LogLevel.Info, source, message);

    public static void Warn(this ILogger logger, string source, string message)
        => logger.Log(LogLevel.Warning, source, message);

    public static void Error(this ILogger logger, string source, string message)
        => logger.Log(LogLevel.Error, source, message);
}
