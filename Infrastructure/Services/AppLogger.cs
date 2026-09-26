using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;

namespace EasyDNS.Infrastructure.Services
{
    public class AppLogger : IAppLogger
    {
        private const int MaxEntries = 300;
        private readonly object _lock = new();

        public ObservableCollection<LogEntry> Logs { get; } = new();
        public event Action? OnLogAdded;

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var entry = new LogEntry(message, level);

            void AddAction()
            {
                lock (_lock)
                {
                    if (Logs.Count >= MaxEntries)
                    {
                        Logs.RemoveAt(0);
                    }
                    Logs.Add(entry);
                }
                OnLogAdded?.Invoke();
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)AddAction);
            }
            else
            {
                AddAction();
            }
        }

        public void LogSuccess(string message) => Log(message, LogLevel.Success);
        public void LogError(string message) => Log(message, LogLevel.Error);
        public void LogWarning(string message) => Log(message, LogLevel.Warning);
        public void LogHighlight(string message) => Log(message, LogLevel.Highlight);

        public void Clear()
        {
            void ClearAction()
            {
                lock (_lock)
                {
                    Logs.Clear();
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)ClearAction);
            }
            else
            {
                ClearAction();
            }
        }
    }
}
