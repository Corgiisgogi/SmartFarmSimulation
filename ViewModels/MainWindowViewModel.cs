using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using SmartFarmUI.Infrastructure;
using SmartFarmUI.Models;
using SmartFarmUI.Services;

namespace SmartFarmUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IPlcService _plcService;
        private readonly IFlaskApiService _flaskApi;
        private readonly ILogService _logService;
        private readonly ISettingsRepository _settingsRepo;

        // === Power / Connection ===
        private bool _powerOn;
        public bool PowerOn { get => _powerOn; set => SetField(ref _powerOn, value); }

        private bool _ethercatPowerOn;
        public bool EthercatPowerOn { get => _ethercatPowerOn; set => SetField(ref _ethercatPowerOn, value); }

        private bool _adsConnected;
        public bool AdsConnected { get => _adsConnected; set => SetField(ref _adsConnected, value); }

        private bool _flaskConnected;
        public bool FlaskConnected { get => _flaskConnected; set => SetField(ref _flaskConnected, value); }

        private string _statusMessage = "준비";
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        // === Farm State ===
        private int _currentFarmId = 1;
        public int CurrentFarmId
        {
            get => _currentFarmId;
            set
            {
                if (SetField(ref _currentFarmId, value))
                    OnPropertyChanged(nameof(CurrentFarm));
            }
        }

        public ObservableCollection<FarmState> Farms { get; } = new ObservableCollection<FarmState>
        {
            new FarmState(1), new FarmState(2), new FarmState(3)
        };

        public FarmState CurrentFarm => _currentFarmId >= 1 && _currentFarmId <= 3
            ? Farms[_currentFarmId - 1] : null;

        // === Sensor Data ===
        private SensorData _currentSensorData = new SensorData();
        public SensorData CurrentSensorData
        {
            get => _currentSensorData;
            private set => SetField(ref _currentSensorData, value);
        }

        // Farm-specific offsets: farmId (1-3) → double[5] (index 1-4 used)
        private readonly Dictionary<int, double[]> _farmOffsets = new Dictionary<int, double[]>
        {
            { 1, new double[5] }, { 2, new double[5] }, { 3, new double[5] }
        };

        // === Lamp States (index 0-3 = Lamp1-4) ===
        public bool[] LampStates { get; } = new bool[4];

        // === Auto-Control ===
        private bool _autoControlEnabled;
        public bool AutoControlEnabled { get => _autoControlEnabled; set => SetField(ref _autoControlEnabled, value); }

        private bool _aiAutoControlEnabled;
        public bool AiAutoControlEnabled { get => _aiAutoControlEnabled; set => SetField(ref _aiAutoControlEnabled, value); }

        private readonly DateTime[] _lastAutoControlTime = new DateTime[5]; // index 1-4
        private static readonly TimeSpan AutoControlCooldown = TimeSpan.FromMinutes(5);

        // === Logs ===
        public ObservableCollection<string> LogEntries { get; } = new ObservableCollection<string>();

        // === Timers ===
        private DispatcherTimer _flaskSendTimer;
        private DispatcherTimer _aiControlTimer;

        // === Commands ===
        public ICommand PowerOnCommand { get; }
        public ICommand PowerOffCommand { get; }
        public ICommand ConnectPlcCommand { get; }
        public ICommand DisconnectPlcCommand { get; }
        public ICommand ConnectFlaskCommand { get; }
        public ICommand SelectFarmCommand { get; }
        public ICommand OpenFarmManageCommand { get; }
        public ICommand ToggleAutoControlCommand { get; }
        public ICommand ToggleAiAutoControlCommand { get; }
        public ICommand ToggleLampCommand { get; }
        public ICommand SetLampOnCommand { get; }
        public ICommand SetLampOffCommand { get; }
        public ICommand OpenLogViewCommand { get; }

        public MainWindowViewModel(IPlcService plcService, IFlaskApiService flaskApi,
            ILogService logService, ISettingsRepository settingsRepo)
        {
            _plcService = plcService;
            _flaskApi = flaskApi;
            _logService = logService;
            _settingsRepo = settingsRepo;

            // PLC events — marshal to UI thread
            _plcService.AnalogDataReceived += data =>
                DispatcherHelper.RunOnUI(() => OnAnalogDataReceived(data));
            _plcService.ConnectionError += msg =>
                DispatcherHelper.RunOnUI(() =>
                {
                    AdsConnected = false;
                    EthercatPowerOn = false;
                    StatusMessage = $"PLC 오류: {msg}";
                    AddLog($"[오류] PLC: {msg}");
                });

            _logService.LogEntryAdded += entry =>
                DispatcherHelper.RunOnUI(() => { if (entry != null) AddLog(entry); });

            // Wire commands
            PowerOnCommand = new RelayCommand(() => PowerOn = true, () => !_powerOn);
            PowerOffCommand = new RelayCommand(ExecutePowerOff, () => _powerOn);
            ConnectPlcCommand = new RelayCommand(async () => await ConnectPlcAsync(), () => _powerOn && !_adsConnected);
            DisconnectPlcCommand = new RelayCommand(DisconnectPlc, () => _adsConnected);
            ConnectFlaskCommand = new RelayCommand(async () => await ConnectFlaskAsync());
            SelectFarmCommand = new RelayCommand<int>(id => CurrentFarmId = id);
            OpenFarmManageCommand = new RelayCommand(OpenFarmManage);
            ToggleAutoControlCommand = new RelayCommand(() => AutoControlEnabled = !_autoControlEnabled);
            ToggleAiAutoControlCommand = new RelayCommand(() => AiAutoControlEnabled = !_aiAutoControlEnabled,
                () => _flaskConnected && _adsConnected);
            ToggleLampCommand = new RelayCommand<int>(index => ToggleLamp(index));
            SetLampOnCommand = new RelayCommand<int>(index => SetLamp(index, true));
            SetLampOffCommand = new RelayCommand<int>(index => SetLamp(index, false));
            OpenLogViewCommand = new RelayCommand(OpenLogView);

            // Setup timers
            _flaskSendTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _flaskSendTimer.Tick += async (s, e) => await SendSensorDataAsync();

            _aiControlTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _aiControlTimer.Tick += async (s, e) => await ExecuteAiAutoControlAsync();

            LoadSettings();
        }

        // === PLC Events ===
        private void OnAnalogDataReceived(AnalogInputData data)
        {
            if (!_powerOn || !_adsConnected) return;

            var offsets = _farmOffsets.TryGetValue(_currentFarmId, out var o) ? o : new double[5];
            var sd = new SensorData
            {
                // Humidity_Sensor → 습도 (index 1)
                RawHumidity = SensorCalibration.ScaleLinear(data.Humidity_Sensor, 0, 32767, 0, 100),
                // Temperature_Sensor → 온도 (index 2)
                RawTemperature = SensorCalibration.ConvertTemperatureCelsius(data.Temperature_Sensor),
                // Pressure_Sensor → 채광 (index 3)
                RawLight = SensorCalibration.ScaleLinear(data.Pressure_Sensor, 0, 32767, 0, 100),
                // Reserve4 → 토양습도 (index 4)
                RawSoilMoisture = SensorCalibration.ScaleLinear(data.Reserve4, 0, 32767, 0, 100),
                LastUpdate = DateTime.Now
            };

            sd.Humidity = SensorCalibration.Clamp(sd.RawHumidity + offsets[1], 0, 100);
            sd.Temperature = SensorCalibration.Clamp(sd.RawTemperature + offsets[2], -20, 60);
            sd.Light = SensorCalibration.Clamp(sd.RawLight + offsets[3], 0, 100);
            sd.SoilMoisture = SensorCalibration.Clamp(sd.RawSoilMoisture + offsets[4], 0, 100);

            CurrentSensorData = sd;
            CheckThresholdsAndAutoControl(sd);
        }

        // === Auto-Control ===
        private void CheckThresholdsAndAutoControl(SensorData sd)
        {
            if (!_autoControlEnabled || CurrentFarm == null) return;

            var thresholds = CurrentFarm.Thresholds;
            if (thresholds == null || thresholds.Length < 5) return;

            // Sensors indexed 1-4
            double[] values = { 0, sd.Humidity, sd.Temperature, sd.Light, sd.SoilMoisture };
            for (int i = 1; i <= 4; i++)
            {
                if (DateTime.Now - _lastAutoControlTime[i] < AutoControlCooldown) continue;
                if (values[i] < thresholds[i].Min)
                {
                    SetLamp(i, true);
                    _lastAutoControlTime[i] = DateTime.Now;
                    AddLog($"[자동제어] 센서{i} 낮음({values[i]:F1}) → 램프{i} ON");
                }
                else if (values[i] > thresholds[i].Max)
                {
                    SetLamp(i, false);
                    _lastAutoControlTime[i] = DateTime.Now;
                    AddLog($"[자동제어] 센서{i} 높음({values[i]:F1}) → 램프{i} OFF");
                }
            }
        }

        private async Task ExecuteAiAutoControlAsync()
        {
            if (!_aiAutoControlEnabled || !_flaskConnected || !_adsConnected) return;
            try
            {
                var sd = _currentSensorData;
                string json = $"{{\"sensor_data\":{{\"humidity\":{sd.Humidity:F1},\"temperature\":{sd.Temperature:F1},\"light\":{sd.Light:F1},\"soil_moisture\":{sd.SoilMoisture:F1}}},\"farm_id\":{_currentFarmId}}}";
                string response = await _flaskApi.RequestAIControlAsync(json);
                if (!string.IsNullOrEmpty(response))
                    ApplyAiCommands(response);
            }
            catch (Exception ex) { AddLog($"[AI오류] {ex.Message}"); }
        }

        private void ApplyAiCommands(string responseJson)
        {
            try
            {
                var jObj = JObject.Parse(responseJson);
                var commands = jObj["commands"] as JArray;
                if (commands == null) return;
                foreach (var cmd in commands)
                {
                    string device = cmd["device"]?.ToString() ?? "";
                    string action = cmd["action"]?.ToString() ?? "";
                    bool on = action.Equals("ON", StringComparison.OrdinalIgnoreCase);
                    int lampIdx = SensorConstants.ResolveSensorIndex(device, 0);
                    if (lampIdx >= 1 && lampIdx <= 4)
                    {
                        SetLamp(lampIdx, on);
                        AddLog($"[AI제어] {device} → {action}");
                    }
                }
            }
            catch (Exception ex) { AddLog($"[AI파싱오류] {ex.Message}"); }
        }

        // === Lamp Control ===
        private void SetLamp(int index, bool on)
        {
            if (index < 1 || index > 4) return;
            LampStates[index - 1] = on;
            OnPropertyChanged(nameof(LampStates));
            try { _plcService.WriteLamp(index, on); } catch { }
        }

        private void ToggleLamp(int index)
        {
            if (index < 1 || index > 4) return;
            SetLamp(index, !LampStates[index - 1]);
        }

        // === Connection ===
        private async Task ConnectPlcAsync()
        {
            StatusMessage = "PLC 연결 중...";
            EthercatPowerOn = true;
            bool connected = await Task.Run(() => _plcService.Connect(msg => DispatcherHelper.RunOnUI(() => AddLog(msg))));
            AdsConnected = connected;
            if (connected)
            {
                StatusMessage = "PLC 연결됨";
                _flaskSendTimer.Start();
                _aiControlTimer.Start();
            }
            else
            {
                StatusMessage = "PLC 연결 실패";
                EthercatPowerOn = false;
            }
        }

        private void DisconnectPlc()
        {
            _plcService.Disconnect(true, msg => DispatcherHelper.RunOnUI(() => AddLog(msg)));
            AdsConnected = false;
            EthercatPowerOn = false;
            _flaskSendTimer.Stop();
            _aiControlTimer.Stop();
            StatusMessage = "PLC 연결 해제됨";
        }

        private async Task ConnectFlaskAsync()
        {
            StatusMessage = "Flask 서버 연결 중...";
            FlaskConnected = await Task.Run(() => _flaskApi.CheckConnection());
            StatusMessage = _flaskConnected ? "Flask 서버 연결됨" : "Flask 서버 연결 실패";
        }

        private void ExecutePowerOff()
        {
            if (_adsConnected) DisconnectPlc();
            PowerOn = false;
            StatusMessage = "전원 OFF";
        }

        // === Flask Data Push ===
        private async Task SendSensorDataAsync()
        {
            if (!_flaskConnected) return;
            try
            {
                var sd = _currentSensorData;
                string json = BuildSensorDataJson(sd);
                await Task.Run(() => _flaskApi.SendSensorData(json));
            }
            catch { }
        }

        private string BuildSensorDataJson(SensorData sd) =>
            $"{{\"currentFarm\":{_currentFarmId},\"powerOn\":{_powerOn.ToString().ToLower()}," +
            $"\"connected\":{_adsConnected.ToString().ToLower()},\"lastUpdate\":\"{sd.LastUpdate:yyyy-MM-ddTHH:mm:ss}\"," +
            $"\"sensors\":[{{\"id\":1,\"name\":\"습도\",\"value\":\"{sd.Humidity:F1}%\",\"rawValue\":{sd.Humidity:F1}}}," +
            $"{{\"id\":2,\"name\":\"온도\",\"value\":\"{sd.Temperature:F1}℃\",\"rawValue\":{sd.Temperature:F1}}}," +
            $"{{\"id\":3,\"name\":\"채광\",\"value\":\"{sd.Light:F1}%\",\"rawValue\":{sd.Light:F1}}}," +
            $"{{\"id\":4,\"name\":\"토양습도\",\"value\":\"{sd.SoilMoisture:F1}%\",\"rawValue\":{sd.SoilMoisture:F1}}}]," +
            $"\"farms\":[{{\"id\":1,\"cropName\":\"{Farms[0].CropName}\",\"note\":\"{Farms[0].Notes}\"}}," +
            $"{{\"id\":2,\"cropName\":\"{Farms[1].CropName}\",\"note\":\"{Farms[1].Notes}\"}}," +
            $"{{\"id\":3,\"cropName\":\"{Farms[2].CropName}\",\"note\":\"{Farms[2].Notes}\"}}]}}";

        // === Farm Management ===
        private void OpenFarmManage()
        {
            var vm = new FarmManageViewModel(_flaskApi, CurrentFarm);
            var dialog = new Views.FarmManageView { DataContext = vm };
            vm.CloseAction = result => { dialog.DialogResult = result; dialog.Close(); };
            if (dialog.ShowDialog() == true && vm.Confirmed)
            {
                if (vm.SelectedCrop != null) CurrentFarm.CropName = vm.SelectedCrop;
                if (vm.ResultNotes != null) CurrentFarm.Notes = vm.ResultNotes;
                SaveSettings();
            }
        }

        private void OpenLogView()
        {
            var vm = new LogViewModel(_logService);
            var win = new Views.LogView { DataContext = vm };
            win.Show();
        }

        // === Settings ===
        private void LoadSettings()
        {
            try
            {
                var thresholds = _settingsRepo.LoadThresholds();
                foreach (var kvp in thresholds)
                {
                    if (kvp.Key < 1 || kvp.Key > 3) continue;
                    var farm = Farms[kvp.Key - 1];
                    // LoadThresholds returns SensorThreshold[] of length SensorCount (4)
                    for (int i = 0; i < kvp.Value.Length && i + 1 < farm.Thresholds.Length; i++)
                    {
                        farm.Thresholds[i + 1].Min = kvp.Value[i].Min;
                        farm.Thresholds[i + 1].Max = kvp.Value[i].Max;
                    }
                }

                var cropNames = new Dictionary<int, string>();
                var notes = new Dictionary<int, string>();
                _settingsRepo.LoadFarmInfo(cropNames, notes);
                for (int i = 1; i <= 3; i++)
                {
                    if (cropNames.TryGetValue(i, out var crop)) Farms[i - 1].CropName = crop;
                    if (notes.TryGetValue(i, out var note)) Farms[i - 1].Notes = note;
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var thresholds = new Dictionary<int, SensorThreshold[]>();
                var cropNames = new Dictionary<int, string>();
                var notes = new Dictionary<int, string>();
                for (int i = 1; i <= 3; i++)
                {
                    var farm = Farms[i - 1];
                    var arr = new SensorThreshold[SensorConstants.SensorCount];
                    for (int j = 0; j < SensorConstants.SensorCount; j++)
                        arr[j] = new SensorThreshold { Min = farm.Thresholds[j + 1].Min, Max = farm.Thresholds[j + 1].Max };
                    thresholds[i] = arr;
                    cropNames[i] = farm.CropName ?? "";
                    notes[i] = farm.Notes ?? "";
                }
                _settingsRepo.SaveThresholds(thresholds);
                _settingsRepo.SaveFarmInfo(cropNames, notes);
            }
            catch { }
        }

        // === Log ===
        private void AddLog(string message)
        {
            if (LogEntries.Count >= 200) LogEntries.RemoveAt(0);
            if (!message.StartsWith("[")) message = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogEntries.Add(message);
        }
    }
}
