using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using SmartFarmUI.Models;
using SmartFarmUI.Services;

namespace SmartFarmUI.Controllers
{
    public class FarmController
    {
        public int CurrentFarmId { get; set; } = 1;
        public bool PowerOn { get; set; }
        public bool AdsConnected { get; set; }

        public Dictionary<int, SensorThreshold[]> FarmSettings { get; } = new Dictionary<int, SensorThreshold[]>();
        public Dictionary<int, string> FarmCropNames { get; } = new Dictionary<int, string>();
        public Dictionary<int, string> FarmNotes { get; } = new Dictionary<int, string>();
        public Dictionary<int, double[]> FarmOffsets { get; } = new Dictionary<int, double[]>();

        public double[] SensorOffsets { get; set; } = new double[SensorConstants.SensorCount + 1];
        public int[] SensorAlertStates { get; } = new int[SensorConstants.SensorCount + 1];

        private readonly object sensorDataLock = new object();
        private SensorData currentSensorData = new SensorData();

        public object SensorDataLock => sensorDataLock;

        public SensorData GetCurrentSensorData()
        {
            lock (sensorDataLock) return currentSensorData;
        }

        public void UpdateSensorData(SensorData data)
        {
            lock (sensorDataLock) currentSensorData = data;
        }

        public string GetSensorDataJson()
        {
            lock (sensorDataLock)
            {
                int min1 = 30, max1 = 70;
                int min2 = 10, max2 = 40;
                int min3 = 30, max3 = 70;
                int min4 = 30, max4 = 70;

                if (FarmSettings.ContainsKey(CurrentFarmId) && FarmSettings[CurrentFarmId] != null)
                {
                    var thresholds = FarmSettings[CurrentFarmId];
                    if (thresholds.Length >= 4)
                    {
                        min1 = thresholds[0].Min; max1 = thresholds[0].Max;
                        min2 = thresholds[1].Min; max2 = thresholds[1].Max;
                        min3 = thresholds[2].Min; max3 = thresholds[2].Max;
                        min4 = thresholds[3].Min; max4 = thresholds[3].Max;
                    }
                }

                var farms = new List<object>();
                for (int farm = 1; farm <= 3; farm++)
                {
                    farms.Add(new
                    {
                        id = farm,
                        cropName = FarmCropNames.ContainsKey(farm) ? FarmCropNames[farm] : "",
                        note = FarmNotes.ContainsKey(farm) ? FarmNotes[farm] : ""
                    });
                }

                var data = new
                {
                    currentFarm = CurrentFarmId,
                    powerOn = PowerOn,
                    connected = AdsConnected,
                    lastUpdate = currentSensorData.LastUpdate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    farms = farms.ToArray(),
                    sensors = new[]
                    {
                        new
                        {
                            id = 1, name = "습도",
                            value = $"{currentSensorData.Humidity:F1}%",
                            rawValue = currentSensorData.Humidity,
                            min = min1, max = max1,
                            percentage = Math.Min(100, Math.Max(0, (int)currentSensorData.Humidity)),
                            status = SensorAlertStates[1] == 0 ? "정상" : (SensorAlertStates[1] < 0 ? "낮음" : "높음"),
                            offset = SensorOffsets[1]
                        },
                        new
                        {
                            id = 2, name = "온도",
                            value = $"{currentSensorData.Temperature:F1}℃",
                            rawValue = currentSensorData.Temperature,
                            min = min2, max = max2,
                            percentage = Math.Min(100, Math.Max(0, (int)((currentSensorData.Temperature - 10) / 30 * 100))),
                            status = SensorAlertStates[2] == 0 ? "정상" : (SensorAlertStates[2] < 0 ? "낮음" : "높음"),
                            offset = SensorOffsets[2]
                        },
                        new
                        {
                            id = 3, name = "채광",
                            value = $"{currentSensorData.Light:F1}%",
                            rawValue = currentSensorData.Light,
                            min = min3, max = max3,
                            percentage = Math.Min(100, Math.Max(0, (int)currentSensorData.Light)),
                            status = SensorAlertStates[3] == 0 ? "정상" : (SensorAlertStates[3] < 0 ? "낮음" : "높음"),
                            offset = SensorOffsets[3]
                        },
                        new
                        {
                            id = 4, name = "토양습도",
                            value = $"{currentSensorData.SoilMoisture:F1}%",
                            rawValue = currentSensorData.SoilMoisture,
                            min = min4, max = max4,
                            percentage = Math.Min(100, Math.Max(0, (int)currentSensorData.SoilMoisture)),
                            status = SensorAlertStates[4] == 0 ? "정상" : (SensorAlertStates[4] < 0 ? "낮음" : "높음"),
                            offset = SensorOffsets[4]
                        }
                    }
                };

                return JsonHelper.ToJson(data);
            }
        }

        public string GetFarmDataJson()
        {
            var data = new
            {
                currentFarm = CurrentFarmId,
                powerOn = PowerOn,
                connected = AdsConnected,
                farms = new[]
                {
                    new { id = 1, cropName = FarmCropNames.ContainsKey(1) ? FarmCropNames[1] : "", note = FarmNotes.ContainsKey(1) ? FarmNotes[1] : "" },
                    new { id = 2, cropName = FarmCropNames.ContainsKey(2) ? FarmCropNames[2] : "", note = FarmNotes.ContainsKey(2) ? FarmNotes[2] : "" },
                    new { id = 3, cropName = FarmCropNames.ContainsKey(3) ? FarmCropNames[3] : "", note = FarmNotes.ContainsKey(3) ? FarmNotes[3] : "" }
                }
            };

            return JsonHelper.ToJson(data);
        }

        public static string GenerateCropDetailedInfo(string cropName, Dictionary<string, object> cropInfo)
        {
            try
            {
                var conditions = JObject.FromObject(cropInfo["conditions"]);
                string description = cropInfo.ContainsKey("description") ? cropInfo["description"]?.ToString() ?? "" : "";
                string baseProduction = cropInfo.ContainsKey("base_production") ? cropInfo["base_production"]?.ToString() ?? "0" : "0";

                var info = new StringBuilder();
                info.AppendLine($"【 {cropName} 재배 최적 환경 】");
                info.AppendLine();

                if (!string.IsNullOrEmpty(description))
                {
                    info.AppendLine(description);
                    info.AppendLine();
                }

                if (conditions["humidity"] != null)
                {
                    var h = conditions["humidity"];
                    info.AppendLine($"• 습도: {h["acceptable_min"]}-{h["acceptable_max"]}% (생육기 최적 범위: {h["optimal_min"]}-{h["optimal_max"]}%)");
                    if ((h["acceptable_min"]?.Value<int>() ?? 0) < 40) info.AppendLine($"  - {h["acceptable_min"]}% 이하: 수분 부족, 생육 저하");
                    if ((h["acceptable_max"]?.Value<int>() ?? 0) > 80) info.AppendLine($"  - {h["acceptable_max"]}% 이상: 병해 발생 위험 증가");
                    info.AppendLine();
                }

                if (conditions["temperature"] != null)
                {
                    var t = conditions["temperature"];
                    info.AppendLine($"• 온도: {t["acceptable_min"]}-{t["acceptable_max"]}℃ (생육기 최적 범위: {t["optimal_min"]}-{t["optimal_max"]}℃)");
                    if ((t["acceptable_min"]?.Value<int>() ?? 0) < 10) info.AppendLine($"  - {t["acceptable_min"]}℃ 이하: 생장 정지, 동해 발생 가능");
                    if ((t["acceptable_max"]?.Value<int>() ?? 0) > 30) info.AppendLine($"  - {t["acceptable_max"]}℃ 이상: 생육 저하, 열 스트레스");
                    info.AppendLine();
                }

                if (conditions["light"] != null)
                {
                    var l = conditions["light"];
                    info.AppendLine($"• 채광: {l["acceptable_min"]}-{l["acceptable_max"]}% (생육기 최적 범위: {l["optimal_min"]}-{l["optimal_max"]}%)");
                    info.AppendLine("  - 광합성 활성화, 생육 촉진");
                    if ((l["acceptable_min"]?.Value<int>() ?? 0) < 50) info.AppendLine($"  - {l["acceptable_min"]}% 이하: 생육 저하, 잎이 연해짐");
                    info.AppendLine();
                }

                if (conditions["soil_moisture"] != null)
                {
                    var s = conditions["soil_moisture"];
                    info.AppendLine($"• 토양습도: {s["acceptable_min"]}-{s["acceptable_max"]}% (생육기 최적 범위: {s["optimal_min"]}-{s["optimal_max"]}%)");
                    if ((s["acceptable_min"]?.Value<int>() ?? 0) < 30) info.AppendLine($"  - {s["acceptable_min"]}% 이하: 뿌리 수분 흡수 어려움, 시들음");
                    if ((s["acceptable_max"]?.Value<int>() ?? 0) > 70) info.AppendLine($"  - {s["acceptable_max"]}% 이상: 뿌리 부패 위험");
                    info.AppendLine();
                }

                if (double.TryParse(baseProduction, out double production) && production > 0)
                {
                    info.AppendLine($"• 예상 생산량: 식물당 약 {production}kg");
                    info.AppendLine();
                }

                info.AppendLine("※ 위 환경 조건을 유지하면 품질과 수확량이 향상됩니다.");
                return info.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 작물 상세 정보 생성 오류: {ex.Message}");
                return cropInfo.ContainsKey("description") ? cropInfo["description"]?.ToString() ?? "" : $"【 {cropName} 재배 】";
            }
        }

        /// <summary>
        /// 작물 조건에서 센서 임계값을 추출하여 반환합니다. UI 업데이트는 호출측에서 수행합니다.
        /// </summary>
        public static SensorThreshold[] ParseCropThresholds(Dictionary<string, object> cropInfo)
        {
            if (!cropInfo.ContainsKey("conditions"))
                return null;

            var conditions = JObject.FromObject(cropInfo["conditions"]);
            var thresholds = new SensorThreshold[SensorConstants.SensorCount];

            thresholds[0] = ParseCondition(conditions["humidity"], 30, 80);
            thresholds[1] = ParseCondition(conditions["temperature"], 0, 35);
            thresholds[2] = ParseCondition(conditions["light"], 50, 80);
            thresholds[3] = ParseCondition(conditions["soil_moisture"], 20, 60);

            return thresholds;
        }

        private static SensorThreshold ParseCondition(JToken condition, int defaultMin, int defaultMax)
        {
            if (condition == null)
                return new SensorThreshold { Min = defaultMin, Max = defaultMax };

            return new SensorThreshold
            {
                Min = condition["acceptable_min"]?.Value<int>() ?? defaultMin,
                Max = condition["acceptable_max"]?.Value<int>() ?? defaultMax
            };
        }
    }
}
