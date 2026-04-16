using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartFarmUI.Models
{
    public class FarmState : INotifyPropertyChanged
    {
        public int FarmId { get; }

        // 인덱스 0 미사용, 1~4 사용 (1=습도, 2=온도, 3=채광, 4=토양습도)
        public ObservableCollection<SensorThreshold> Thresholds { get; }

        private string _cropName;
        public string CropName
        {
            get => _cropName;
            set { if (_cropName != value) { _cropName = value; OnPropertyChanged(); } }
        }

        private string _notes;
        public string Notes
        {
            get => _notes;
            set { if (_notes != value) { _notes = value; OnPropertyChanged(); } }
        }

        public double[] SensorOffsets { get; set; } // 인덱스 0 미사용, 1~4 사용

        public FarmState(int farmId)
        {
            FarmId = farmId;
            Thresholds = new ObservableCollection<SensorThreshold>();
            for (int i = 0; i <= SensorConstants.SensorCount; i++)
                Thresholds.Add(new SensorThreshold());
            CropName = "";
            Notes = "";
            SensorOffsets = new double[SensorConstants.SensorCount + 1];
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
