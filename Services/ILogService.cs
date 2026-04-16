using System;
using System.Collections.Generic;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public interface ILogService
    {
        event Action<string> LogEntryAdded;

        IReadOnlyList<string> LogHistory { get; }

        void Log(string message);
        void LogWarning(string message);
        void LogSensorData(SensorData data, string status = "정상");

        string[] GetLogEntries();
        void ClearLogs();

        string GetLogsJson();
        bool SaveLogsToFile(string requestBody);
        string LoadLogsFromFile(string filePath);
        string ParseLogsFromContent(string fileContent);
        bool AddLogsToHistory(string logsJson);
    }
}
