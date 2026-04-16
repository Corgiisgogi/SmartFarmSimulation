using System;

namespace SmartFarmUI.Models
{
    public class SensorData
    {
        public double Humidity { get; set; }
        public double Temperature { get; set; }
        public double Light { get; set; }
        public double SoilMoisture { get; set; }
        public DateTime LastUpdate { get; set; }
        // 원본 센서 값 (오프셋 적용 전)
        public double RawHumidity { get; set; }
        public double RawTemperature { get; set; }
        public double RawLight { get; set; }
        public double RawSoilMoisture { get; set; }
    }
}
