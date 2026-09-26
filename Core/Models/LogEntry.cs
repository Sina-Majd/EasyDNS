using System;

namespace EasyDNS.Core.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Highlight
    }

    public class LogEntry
    {
        public string Timestamp { get; }
        public string Message { get; }
        public LogLevel Level { get; }

        public LogEntry(string message, LogLevel level = LogLevel.Info)
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
            Message = message;
            Level = level;
        }
    }
}
