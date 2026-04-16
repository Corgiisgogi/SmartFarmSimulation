using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartFarmUI.Models
{
    public class SensorData : INotifyPropertyChanged
    {
        private double _humidity;
        public double Humidity
        {
            get => _humidity;
            set { if (_humidity != value) { _humidity = value; OnPropertyChanged(); } }
        }

        private double _temperature;
        public double Temperature
        {
            get => _temperature;
            set { if (_temperature != value) { _temperature = value; OnPropertyChanged(); } }
        }

        private double _light;
        public double Light
        {
            get => _light;
            set { if (_light != value) { _light = value; OnPropertyChanged(); } }
        }

        private double _soilMoisture;
        public double SoilMoisture
        {
            get => _soilMoisture;
            set { if (_soilMoisture != value) { _soilMoisture = value; OnPropertyChanged(); } }
        }

        private DateTime _lastUpdate;
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set { if (_lastUpdate != value) { _lastUpdate = value; OnPropertyChanged(); } }
        }

        // 원본 센서 값 (오프셋 적용 전)
        private double _rawHumidity;
        public double RawHumidity
        {
            get => _rawHumidity;
            set { if (_rawHumidity != value) { _rawHumidity = value; OnPropertyChanged(); } }
        }

        private double _rawTemperature;
        public double RawTemperature
        {
            get => _rawTemperature;
            set { if (_rawTemperature != value) { _rawTemperature = value; OnPropertyChanged(); } }
        }

        private double _rawLight;
        public double RawLight
        {
            get => _rawLight;
            set { if (_rawLight != value) { _rawLight = value; OnPropertyChanged(); } }
        }

        private double _rawSoilMoisture;
        public double RawSoilMoisture
        {
            get => _rawSoilMoisture;
            set { if (_rawSoilMoisture != value) { _rawSoilMoisture = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
