using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SmartFarmUI.Infrastructure;
using SmartFarmUI.Models;
using SmartFarmUI.Services;

namespace SmartFarmUI.ViewModels
{
    public class FarmManageViewModel : ViewModelBase
    {
        private readonly IFlaskApiService _flaskApi;

        public ObservableCollection<string> AvailableCrops { get; } = new ObservableCollection<string>();

        private string _selectedCrop;
        public string SelectedCrop { get => _selectedCrop; set => SetField(ref _selectedCrop, value); }

        private string _notes;
        public string Notes { get => _notes; set => SetField(ref _notes, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        public bool Confirmed { get; private set; }
        public string ResultNotes { get; private set; }

        public ICommand LoadCropsCommand { get; }
        public ICommand OpenAddCropCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<bool> CloseAction { get; set; }

        public FarmManageViewModel(IFlaskApiService flaskApi, FarmState currentFarm)
        {
            _flaskApi = flaskApi;
            _selectedCrop = currentFarm?.CropName ?? "기본";
            _notes = currentFarm?.Notes ?? "";

            foreach (var crop in new[] { "사과", "토마토", "상추", "딸기", "오이", "고추", "배추", "시금치", "파프리카", "가지", "무", "브로콜리", "기본" })
                AvailableCrops.Add(crop);

            LoadCropsCommand = new RelayCommand(async () => await LoadCropsAsync());
            OpenAddCropCommand = new RelayCommand(ExecuteOpenAddCrop);
            ConfirmCommand = new RelayCommand(ExecuteConfirm);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke(false));

            Task.Run(async () => await LoadCropsAsync());
        }

        private async Task LoadCropsAsync()
        {
            IsLoading = true;
            try
            {
                var crops = await Task.Run(() => _flaskApi.GetCropList());
                DispatcherHelper.RunOnUI(() =>
                {
                    AvailableCrops.Clear();
                    foreach (var crop in crops) AvailableCrops.Add(crop);
                    if (!string.IsNullOrEmpty(_selectedCrop) && !AvailableCrops.Contains(_selectedCrop))
                        AvailableCrops.Add(_selectedCrop);
                    StatusMessage = "";
                });
            }
            catch
            {
                DispatcherHelper.RunOnUI(() => StatusMessage = "서버 연결 실패");
            }
            finally { IsLoading = false; }
        }

        private void ExecuteOpenAddCrop()
        {
            var vm = new AddCropViewModel(_flaskApi);
            var dialog = new Views.AddCropView { DataContext = vm };
            vm.CloseAction = result => { dialog.DialogResult = result; dialog.Close(); };
            if (dialog.ShowDialog() == true)
                Task.Run(async () => await LoadCropsAsync());
        }

        private void ExecuteConfirm()
        {
            Confirmed = true;
            ResultNotes = Notes;
            CloseAction?.Invoke(true);
        }
    }
}
