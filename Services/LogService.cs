using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public class LogService : ILogService
    {
        private readonly List<string> logHistory = new List<string>();

        public event Action<string> LogEntryAdded;

        public IReadOnlyList<string> LogHistory => logHistory;

        public void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{time}] {message}";
            logHistory.Add(logEntry);
            LogEntryAdded?.Invoke(logEntry);
        }

        public void LogWarning(string message)
        {
            Log($"⚠️ {message}");
        }

        public void LogSensorData(SensorData data, string status = "정상")
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string statusIcon = status == "정상" ? "✅" : "⚠️";

            string logEntry = $"[{time}] {statusIcon} 센서데이터 " +
                             $"습도:{data.Humidity:F1}% " +
                             $"온도:{data.Temperature:F1}℃ " +
                             $"채광:{data.Light:F1}% " +
                             $"토양습도:{data.SoilMoisture:F1}% " +
                             $"상태:{status}";

            logHistory.Add(logEntry);
            LogEntryAdded?.Invoke(logEntry);
        }

        public string[] GetLogEntries()
        {
            return logHistory.ToArray();
        }

        public void ClearLogs()
        {
            logHistory.Clear();
        }

        public string GetLogsJson()
        {
            var aiRelevantLogs = logHistory.Where(log => IsAIRelevantLog(log))
                .Select(log => new
                {
                    timestamp = log.Length > 10 ? log.Substring(1, 8) : DateTime.Now.ToString("HH:mm:ss"),
                    message = log.Length > 11 ? log.Substring(11) : log,
                    date = DateTime.Now.ToString("yyyy-MM-dd")
                }).Take(500).ToList();

            return JsonHelper.ToJson(aiRelevantLogs);
        }

        public bool SaveLogsToFile(string requestBody)
        {
            try
            {
                string filePath = "";
                List<string> logsToSave;

                if (requestBody.Trim().StartsWith("{"))
                {
                    int filePathStart = requestBody.IndexOf("\"filePath\"");
                    if (filePathStart >= 0)
                    {
                        int colonIndex = requestBody.IndexOf(":", filePathStart);
                        int quoteStart = requestBody.IndexOf("\"", colonIndex) + 1;
                        int quoteEnd = requestBody.IndexOf("\"", quoteStart);
                        if (quoteEnd > quoteStart)
                        {
                            filePath = requestBody.Substring(quoteStart, quoteEnd - quoteStart);
                        }
                    }

                    int logsStart = requestBody.IndexOf("\"logs\"");
                    if (logsStart >= 0)
                    {
                        int arrayStart = requestBody.IndexOf("[", logsStart);
                        if (arrayStart >= 0)
                        {
                            logsToSave = logHistory.ToList();
                        }
                        else
                        {
                            logsToSave = logHistory.ToList();
                        }
                    }
                    else
                    {
                        logsToSave = logHistory.ToList();
                    }
                }
                else
                {
                    filePath = requestBody.Trim().Trim('"', '\'', ' ');
                    logsToSave = logHistory.ToList();
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    string defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    if (!Directory.Exists(defaultDir))
                        Directory.CreateDirectory(defaultDir);
                    filePath = Path.Combine(defaultDir, $"SmartFarm_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                }

                File.WriteAllLines(filePath, logsToSave, System.Text.Encoding.UTF8);
                Log($"로그 파일 저장 완료: {filePath} ({logsToSave.Count}개 항목)");
                return true;
            }
            catch (Exception ex)
            {
                Log($"로그 파일 저장 실패: {ex.Message}");
                return false;
            }
        }

        public string LoadLogsFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return "{\"error\":\"파일을 찾을 수 없습니다.\"}";
                }

                var loadedLogs = File.ReadAllLines(filePath, System.Text.Encoding.UTF8).ToList();

                var filteredLogs = loadedLogs.Where(log => IsAIRelevantLog(log)).ToList();

                var logsJson = filteredLogs.Select(log => new
                {
                    timestamp = log.Length > 10 ? log.Substring(1, 8) : DateTime.Now.ToString("HH:mm:ss"),
                    message = log.Length > 11 ? log.Substring(11) : log,
                    date = DateTime.Now.ToString("yyyy-MM-dd")
                }).Take(500).ToList();

                return JsonHelper.ToJson(logsJson);
            }
            catch (Exception ex)
            {
                Log($"로그 파일 불러오기 실패: {ex.Message}");
                return $"{{\"error\":\"{JsonHelper.EscapeJson(ex.Message)}\"}}";
            }
        }

        public string ParseLogsFromContent(string fileContent)
        {
            try
            {
                if (string.IsNullOrEmpty(fileContent))
                {
                    return "[]";
                }

                var allLines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                var filteredLines = allLines.Where(log => IsAIRelevantLog(log)).ToList();

                var logsJson = filteredLines.Select(log => new
                {
                    timestamp = log.Length > 10 && log.StartsWith("[") ? log.Substring(1, 8) : DateTime.Now.ToString("HH:mm:ss"),
                    message = log.Length > 11 && log.StartsWith("[") ? log.Substring(11).Trim() : log.Trim(),
                    date = DateTime.Now.ToString("yyyy-MM-dd")
                }).ToList();

                return JsonHelper.ToJson(logsJson);
            }
            catch (Exception ex)
            {
                Log($"로그 파일 내용 파싱 실패: {ex.Message}");
                return $"{{\"error\":\"{JsonHelper.EscapeJson(ex.Message)}\"}}";
            }
        }

        public bool AddLogsToHistory(string logsJson)
        {
            try
            {
                if (string.IsNullOrEmpty(logsJson))
                {
                    return false;
                }

                var lines = new List<string>();

                int startIndex = logsJson.IndexOf('[');
                int endIndex = logsJson.LastIndexOf(']');
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string arrayContent = logsJson.Substring(startIndex + 1, endIndex - startIndex - 1);

                    int braceCount = 0;
                    int objStart = -1;
                    for (int i = 0; i < arrayContent.Length; i++)
                    {
                        if (arrayContent[i] == '{')
                        {
                            if (braceCount == 0) objStart = i;
                            braceCount++;
                        }
                        else if (arrayContent[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0 && objStart >= 0)
                            {
                                string objStr = arrayContent.Substring(objStart, i - objStart + 1);

                                string timestamp = "";
                                string message = "";

                                int timestampIndex = objStr.IndexOf("\"timestamp\"");
                                if (timestampIndex >= 0)
                                {
                                    int colonIndex = objStr.IndexOf(":", timestampIndex);
                                    int quoteStart = objStr.IndexOf("\"", colonIndex) + 1;
                                    int quoteEnd = objStr.IndexOf("\"", quoteStart);
                                    if (quoteEnd > quoteStart)
                                    {
                                        timestamp = objStr.Substring(quoteStart, quoteEnd - quoteStart);
                                    }
                                }

                                int messageIndex = objStr.IndexOf("\"message\"");
                                if (messageIndex >= 0)
                                {
                                    int colonIndex = objStr.IndexOf(":", messageIndex);
                                    int quoteStart = objStr.IndexOf("\"", colonIndex) + 1;
                                    int quoteEnd = objStr.IndexOf("\"", quoteStart);
                                    if (quoteEnd > quoteStart)
                                    {
                                        message = objStr.Substring(quoteStart, quoteEnd - quoteStart);
                                    }
                                }

                                if (!string.IsNullOrEmpty(message))
                                {
                                    string logEntry = $"[{timestamp}] {message}";
                                    lines.Add(logEntry);
                                }

                                objStart = -1;
                            }
                        }
                    }
                }

                lock (logHistory)
                {
                    foreach (var line in lines)
                    {
                        if (!logHistory.Contains(line))
                        {
                            logHistory.Add(line);
                        }
                    }
                }

                LogEntryAdded?.Invoke(null); // signal UI refresh
                Log($"로그 추가 완료: {lines.Count}개 항목 추가");
                return true;
            }
            catch (Exception ex)
            {
                Log($"로그 추가 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AI 분석에 관련된 로그인지 판별 (3곳에서 중복되던 필터링 로직 통합)
        /// </summary>
        private static bool IsAIRelevantLog(string log)
        {
            if (string.IsNullOrWhiteSpace(log)) return false;

            // 제외할 로그
            if (log.Contains("스마트팜") && (log.Contains("선택") || log.Contains("번 선택")))
                return false;
            if (log.Contains("로그 저장 완료") || log.Contains("로그 저장 실패"))
                return false;
            if (log.Contains("로그 불러오기 완료") || log.Contains("로그 불러오기 실패"))
                return false;
            if (log.Contains("로그 추가 완료") || log.Contains("로그 추가 실패"))
                return false;
            if (log.Contains("로그가 모두 지워졌습니다"))
                return false;
            if (log.Contains("웹서버:") && (log.Contains("웹 서버 시작") || log.Contains("웹 서버 중지")))
                return false;

            // 포함할 로그
            return log.Contains("센서") ||
                   log.Contains("습도") ||
                   log.Contains("온도") ||
                   log.Contains("채광") ||
                   log.Contains("토양") ||
                   log.Contains("⚠️") ||
                   log.Contains("오류") ||
                   log.Contains("에러") ||
                   log.Contains("경고") ||
                   log.Contains("주의") ||
                   log.Contains("정상 복귀") ||
                   log.Contains("✅") ||
                   log.Contains("전원") && (log.Contains("켜짐") || log.Contains("꺼짐") || log.Contains("OFF") || log.Contains("ON")) ||
                   log.Contains("연결") && (log.Contains("연결됨") || log.Contains("연결 실패") || log.Contains("연결 안됨")) ||
                   log.Contains("EtherCAT") ||
                   log.Contains("Error") ||
                   log.Contains("Warning") ||
                   log.Contains("재배 작물") ||
                   log.Contains("정보:");
        }
    }
}
