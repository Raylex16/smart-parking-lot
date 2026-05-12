using System;
using System.Collections.Generic;
using SmartParkingLot.Core.Interfaces;

namespace SmartParkingLot.Gui.Bootstrap;

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Source, string Message)
{
    public string Formatted =>
        $"[{Timestamp:HH:mm:ss.fff}][{Tag(Level)}][{Source}] {Message}";

    private static string Tag(LogLevel l) => l switch
    {
        LogLevel.Debug   => "DBG",
        LogLevel.Info    => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error   => "ERR",
        _ => "LOG"
    };
}

/// In-memory ring-buffer logger that the GUI subscribes to for the live monitor view.
public sealed class GuiLogger : ILogger
{
    private const int CAPACITY = 500;
    private readonly Queue<LogEntry> _buffer = new(CAPACITY);
    private readonly object _lock = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public event Action<LogEntry>? Appended;

    public void Log(LogLevel level, string src, string msg)
    {
        if (level < MinimumLevel) return;
        var entry = new LogEntry(DateTime.Now, level, src, msg);
        lock (_lock)
        {
            if (_buffer.Count == CAPACITY) _buffer.Dequeue();
            _buffer.Enqueue(entry);
        }
        Appended?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_lock) return _buffer.ToArray();
    }
}
