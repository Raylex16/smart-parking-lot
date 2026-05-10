namespace SmartParkingLot.Core.Interfaces;

public enum LogLevel { Debug, Info, Warning, Error }

public interface ILogger
{
    void Log(LogLevel level, string src, string msg);
}