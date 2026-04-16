using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartFarmUI.Models
{
    public class SensorThreshold : INotifyPropertyChanged
    {
        private int _min;
        public int Min
        {
            get => _min;
            set { if (_min != value) { _min = value; OnPropertyChanged(); } }
        }

        private int _max;
        public int Max
        {
            get => _max;
            set { if (_max != value) { _max = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
