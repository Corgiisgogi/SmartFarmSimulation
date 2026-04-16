using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly string settingsFilePath;
        private readonly string farmInfoFilePath;

        public SettingsRepository()
        {
            settingsFilePath = Path.Combine(Application.StartupPath, "sensor_thresholds.txt");
            farmInfoFilePath = Path.Combine(Application.StartupPath, "farm_info.txt");
        }

        public void SaveThresholds(Dictionary<int, SensorThreshold[]> farmSettings)
        {
            try
            {
                var lines = new List<string>();

                foreach (var farm in farmSettings.Keys.OrderBy(f => f))
                {
                    if (farmSettings.TryGetValue(farm, out var thresholds))
                    {
                        var thresholdValues = new List<string>();
                        for (int i = 0; i < thresholds.Length; i++)
                        {
                            thresholdValues.Add($"S{i + 1}Min={thresholds[i].Min},S{i + 1}Max={thresholds[i].Max}");
                        }
                        lines.Add($"Farm{farm}:{string.Join(";", thresholdValues)}");
                    }
                }

                File.WriteAllLines(settingsFilePath, lines, Encoding.UTF8);
            }
            catch (Exception)
            {
                // 호출측에서 로그 처리
                throw;
            }
        }

        public Dictionary<int, SensorThreshold[]> LoadThresholds()
        {
            var result = new Dictionary<int, SensorThreshold[]>();

            if (!File.Exists(settingsFilePath))
                return result;

            var lines = File.ReadAllLines(settingsFilePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains(":"))
                    continue;

                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                    continue;

                if (!parts[0].StartsWith("Farm") || !int.TryParse(parts[0].Substring(4), out int farmId))
                    continue;

                var sensorValues = parts[1].Split(';');
                var thresholds = new SensorThreshold[SensorConstants.SensorCount];

                for (int i = 0; i < SensorConstants.SensorCount && i < sensorValues.Length; i++)
                {
                    var sensorData = sensorValues[i];
                    int min = 0, max = 100;

                    var pairs = sensorData.Split(',');
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=');
                        if (kv.Length == 2)
                        {
                            if (kv[0].EndsWith("Min") && int.TryParse(kv[1], out int minVal))
                                min = minVal;
                            else if (kv[0].EndsWith("Max") && int.TryParse(kv[1], out int maxVal))
                                max = maxVal;
                        }
                    }

                    thresholds[i] = new SensorThreshold { Min = min, Max = max };
                }

                result[farmId] = thresholds;
            }

            return result;
        }

        public void SaveFarmInfo(Dictionary<int, string> farmCropNames, Dictionary<int, string> farmNotes)
        {
            try
            {
                var lines = new List<string>();

                foreach (var farm in farmCropNames.Keys.Union(farmNotes.Keys).OrderBy(f => f))
                {
                    string cropName = farmCropNames.ContainsKey(farm) ? farmCropNames[farm] : "";
                    string note = farmNotes.ContainsKey(farm) ? farmNotes[farm] : "";

                    string encodedNote = note.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

                    lines.Add($"Farm{farm}:Crop={cropName};Note={encodedNote}");
                }

                File.WriteAllLines(farmInfoFilePath, lines, Encoding.UTF8);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void LoadFarmInfo(Dictionary<int, string> farmCropNames, Dictionary<int, string> farmNotes)
        {
            if (!File.Exists(farmInfoFilePath))
                return;

            var lines = File.ReadAllLines(farmInfoFilePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains(":"))
                    continue;

                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                    continue;

                if (!parts[0].StartsWith("Farm") || !int.TryParse(parts[0].Substring(4), out int farmId))
                    continue;

                var infoParts = parts[1].Split(';');
                string cropName = "";
                string note = "";

                foreach (var infoPart in infoParts)
                {
                    if (infoPart.StartsWith("Crop="))
                    {
                        cropName = infoPart.Substring(5);
                    }
                    else if (infoPart.StartsWith("Note="))
                    {
                        note = infoPart.Substring(5);
                        note = note.Replace("\\n", Environment.NewLine);
                    }
                }

                if (!string.IsNullOrEmpty(cropName))
                    farmCropNames[farmId] = cropName;

                if (!string.IsNullOrEmpty(note))
                    farmNotes[farmId] = note;
            }
        }
    }
}
