using System;
using System.Collections.ObjectModel;
using EasyDNS.Core.Models;

namespace EasyDNS.Core.Interfaces
{
    public interface IAppLogger
    {
        ObservableCollection<LogEntry> Logs { get; }
        event Action? OnLogAdded;
        void Log(string message, LogLevel level = LogLevel.Info);
        void LogSuccess(string message);
        void LogError(string message);
        void LogWarning(string message);
        void LogHighlight(string message);
        void Clear();
    }
}
