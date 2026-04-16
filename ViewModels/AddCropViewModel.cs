using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SmartFarmUI.Infrastructure;
using SmartFarmUI.Services;

namespace SmartFarmUI.ViewModels
{
    public class AddCropViewModel : ViewModelBase
    {
        private readonly IFlaskApiService _flaskApi;

        private string _cropName = "";
        public string CropName
        {
            get => _cropName;
            set { SetField(ref _cropName, value); SubmitCommand.RaiseCanExecuteChanged(); }
        }

        private string _description = "";
        public string Description { get => _description; set => SetField(ref _description, value); }

        private double _baseProduction = 10;
        public double BaseProduction { get => _baseProduction; set => SetField(ref _baseProduction, value); }

        private int _humidityMin = 40; public int HumidityMin { get => _humidityMin; set => SetField(ref _humidityMin, value); }
        private int _humidityMax = 80; public int HumidityMax { get => _humidityMax; set => SetField(ref _humidityMax, value); }
        private int _tempMin = 15; public int TempMin { get => _tempMin; set => SetField(ref _tempMin, value); }
        private int _tempMax = 30; public int TempMax { get => _tempMax; set => SetField(ref _tempMax, value); }
        private int _lightMin = 50; public int LightMin { get => _lightMin; set => SetField(ref _lightMin, value); }
        private int _lightMax = 90; public int LightMax { get => _lightMax; set => SetField(ref _lightMax, value); }
        private int _soilMin = 40; public int SoilMin { get => _soilMin; set => SetField(ref _soilMin, value); }
        private int _soilMax = 80; public int SoilMax { get => _soilMax; set => SetField(ref _soilMax, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { SetField(ref _isBusy, value); SubmitCommand.RaiseCanExecuteChanged(); } }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        public RelayCommand SubmitCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Action<bool> CloseAction { get; set; }

        public AddCropViewModel(IFlaskApiService flaskApi)
        {
            _flaskApi = flaskApi;
            SubmitCommand = new RelayCommand(async () => await ExecuteSubmitAsync(),
                () => !string.IsNullOrWhiteSpace(_cropName) && !_isBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke(false));
        }

        private async Task ExecuteSubmitAsync()
        {
            IsBusy = true;
            StatusMessage = "작물 추가 중...";
            try
            {
                string json = $"{{\"name\":\"{_cropName}\",\"description\":\"{_description}\",\"base_production\":{_baseProduction}," +
                    $"\"conditions\":{{\"humidity\":{{\"optimal_min\":{_humidityMin},\"optimal_max\":{_humidityMax}," +
                    $"\"acceptable_min\":{Math.Max(0, _humidityMin - 10)},\"acceptable_max\":{Math.Min(100, _humidityMax + 10)}}}," +
                    $"\"temperature\":{{\"optimal_min\":{_tempMin},\"optimal_max\":{_tempMax}," +
                    $"\"acceptable_min\":{Math.Max(0, _tempMin - 5)},\"acceptable_max\":{Math.Min(50, _tempMax + 5)}}}," +
                    $"\"light\":{{\"optimal_min\":{_lightMin},\"optimal_max\":{_lightMax}," +
                    $"\"acceptable_min\":{Math.Max(0, _lightMin - 10)},\"acceptable_max\":{Math.Min(100, _lightMax + 10)}}}," +
                    $"\"soil_moisture\":{{\"optimal_min\":{_soilMin},\"optimal_max\":{_soilMax}," +
                    $"\"acceptable_min\":{Math.Max(0, _soilMin - 10)},\"acceptable_max\":{Math.Min(100, _soilMax + 10)}}}}}}}";

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("http://localhost:5000/api/crops", content);
                    if (response.IsSuccessStatusCode)
                    {
                        StatusMessage = "작물이 추가되었습니다!";
                        await Task.Delay(500);
                        CloseAction?.Invoke(true);
                    }
                    else { StatusMessage = $"오류: {response.StatusCode}"; }
                }
            }
            catch (Exception ex) { StatusMessage = $"오류: {ex.Message}"; }
            finally { IsBusy = false; }
        }
    }
}
