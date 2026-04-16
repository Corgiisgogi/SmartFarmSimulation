using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TwinCAT.Ads;
using SmartFarmUI.Models;
using SmartFarmUI.Services;
using SmartFarmUI.Controllers;

namespace SmartFarmUI
{
    public partial class Form1 : Form
    {
        private const int SensorCount = 4;

        // SensorThreshold moved to Models/SensorThreshold.cs

        private bool powerOn = false;
        private bool ethercatPowerOn = false;
        private int currentFarm = 1;
        private readonly ILogService logService = new LogService();
        private readonly ISettingsRepository settingsRepo = new SettingsRepository();
        private readonly IFlaskApiService flaskApi = new FlaskApiService();
        private readonly IPlcService plcService = new PlcService();
        private LogForm logForm;
        private readonly Dictionary<int, SensorThreshold[]> farmSettings = new Dictionary<int, SensorThreshold[]>();
        private readonly Dictionary<int, string> farmCropNames = new Dictionary<int, string>();
        private readonly Dictionary<int, string> farmNotes = new Dictionary<int, string>();
        
        // 설정 파일 경로는 SettingsRepository로 이동

        private readonly string[] sensorDisplayNames = { "", "습도", "온도", "채광", "토양습도" };
        private readonly int[] sensorAlertStates = new int[SensorCount + 1];
        private readonly double[] sensorOffsets = new double[SensorCount + 1]; // UI에서 설정한 오프셋 값 (+/-)
        private readonly Dictionary<int, double[]> farmOffsets = new Dictionary<int, double[]>(); // 농장별 오프셋 저장
        
        
        // 초기 연결 후 경고 표시 지연 (센서 안정화 시간)
        private DateTime sensorInitializationTime = DateTime.MinValue;
        private readonly TimeSpan sensorStabilizationTime = TimeSpan.FromSeconds(5); // 5초 동안 경고 표시 안 함
        
        // 자동 제어 시스템 관련
        private bool autoControlEnabled = false;
        private Dictionary<int, DateTime> lastAutoControlTime = new Dictionary<int, DateTime>(); // 센서별 마지막 자동 제어 시간
        private readonly TimeSpan autoControlCooldown = TimeSpan.FromMinutes(5); // 5분 쿨다운
        
        // AI 자동 제어 활성화 상태 (외부 요인으로 값이 변하면 자동으로 최적값으로 맞춤)
        private bool aiAutoControlEnabled = false;
        private DateTime lastAIAutoControlCheck = DateTime.MinValue;
        private DateTime lastAIAutoControlExecution = DateTime.MinValue;
        private readonly TimeSpan aiAutoControlCheckInterval = TimeSpan.FromSeconds(3); // 3초마다 체크 및 제어
        private readonly TimeSpan aiAutoControlExecutionCooldown = TimeSpan.FromSeconds(3); // 3초 쿨다운 (빠른 반응성)
        
        // Flask 웹 서버 관련 - flaskServerRunning은 flaskApi.IsConnected로 위임
        private bool flaskServerRunning { get => flaskApi.IsConnected; }
        private const string FlaskServerUrl = "http://localhost:5000";
        private readonly object sensorDataLock = new object();
        private SensorData currentSensorData = new SensorData();

        // PLC 필드는 PlcService로 이동. adsConnected는 UI 상태 동기화용으로 유지
        private bool adsConnected = false;
        
        // 정기 센서 데이터 로깅용 Timer (웹 표준 형식)
        private System.Threading.Timer sensorLogTimer;
        private DateTime lastSensorLogTime = DateTime.MinValue;
        private readonly TimeSpan sensorLogInterval = TimeSpan.FromMinutes(5); // 5분마다 정기 로깅
        
        // 압력 센서 디버깅 로그용 (주기적으로만 출력)
        private DateTime lastPressureLogTime = DateTime.MinValue;
        
        // ADS 상수는 SensorConstants로 이동
        
        // 버튼 엣지 감지를 위한 이전 상태 (초기값은 모두 false)
        private bool[] previousButtonStates = new bool[5]; // Button1~4 + 인덱스 0은 사용 안 함
        private bool buttonStatesInitialized = false; // 버튼 상태 초기화 플래그

        // EthercatConnectionStatus moved to Models/PlcDataStructs.cs
        private EthercatConnectionStatus ethercatStatus = EthercatConnectionStatus.Off;

        public Form1()
        {
            InitializeComponent();
            InitializeSensorUI();
            InitializeFarmSettings();
            UpdateFarmButtonStyles();
            SetEthercatStatus(EthercatConnectionStatus.Off);
            SetOperationalControlsEnabled(false);

            // LogService 이벤트 구독
            logService.LogEntryAdded += OnLogEntryAdded;

            // PlcService 이벤트 구독
            plcService.AnalogDataReceived += (data) => BeginInvoke(new Action(() => UpdateSensorsFromPlc(data)));
            plcService.DigitalInputReceived += (data) => BeginInvoke(new Action(() => HandleDigitalInput(data)));
            plcService.ConnectionError += (msg) => BeginInvoke(new Action(() =>
            {
                Log($"PLC 데이터 읽기 실패: {msg}");
                MessageBox.Show($"PLC 데이터 읽기 실패: {msg}", "PLC 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DisconnectFromPlc(false);
                ethercatPowerOn = false;
                SetEthercatStatus(EthercatConnectionStatus.Error);
            }));

            // Flask 서버 연결은 별도 스레드에서 확인
            Task.Run(() => CheckFlaskServerConnection());
            
            
            // 자동 제어 시스템 초기화
            InitializeAutoControlSystem();
            
            // 자동 제어 및 알림 UI 초기화 (폼 로드 후 호출)
            this.Load += Form1_Load;
            
            // 저장된 센서 임계값 불러오기 (임계값만 불러옴, 실제 센서 값은 PLC에서 직접 받음)
            LoadSensorThresholds();
            
            // 저장된 스마트팜 정보 불러오기
            LoadFarmInfo();
            
            Log("시스템 준비 완료");
            Log("🍎 스마트팜 1번: 사과 재배 최적 환경 설정 완료 (임계값만 설정, 실제 센서 값은 PLC에서 직접 수신)");
        }
        
        // 자동 제어 시스템 초기화
        private void InitializeAutoControlSystem()
        {
            for (int i = 1; i <= SensorCount; i++)
            {
                lastAutoControlTime[i] = DateTime.MinValue;
            }
        }
        
        private Button btnAutoControl;
        
        private void InitializeAutoControlUI()
        {
            // 이미 초기화되었으면 스킵
            if (btnAutoControl != null)
                return;
            
            if (grpBottomButtons == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ grpBottomButtons가 null입니다!");
                return;
            }
            
            // 자동 제어 버튼 추가
            if (btnAutoControl == null)
            {
                btnAutoControl = new Button
                {
                    Text = "AI 자동제어",
                    Size = new Size(145, 35),
                    UseVisualStyleBackColor = true,
                    TabIndex = 3,
                    BackColor = Color.LightBlue
                };
                
                if (btnWebConnect != null)
                {
                    btnAutoControl.Font = btnWebConnect.Font;
                    // 웹 연결 버튼 오른쪽에 배치
                    btnAutoControl.Location = new Point(btnWebConnect.Right + 10, btnWebConnect.Top);
                    btnAutoControl.Size = new Size(btnWebConnect.Width, btnWebConnect.Height);  // 웹 연결 버튼과 같은 크기
                }
                else
                {
                    // 웹 연결 버튼이 없을 경우 대비
                    btnAutoControl.Location = new Point(165, 85);
                }
                btnAutoControl.Click += BtnAutoControl_Click;
                grpBottomButtons.Controls.Add(btnAutoControl);
            }
        }
        
        private void BtnAutoControl_Click(object sender, EventArgs e)
        {
            // 전원과 연결 상태 확인
            if (!powerOn || !adsConnected)
            {
                MessageBox.Show("전원을 켜고 장비에 연결한 후 사용할 수 있습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Flask 서버 연결 확인
            if (!flaskServerRunning)
            {
                MessageBox.Show("Flask 서버에 연결할 수 없습니다.\n웹 연결 버튼을 먼저 클릭하여 서버를 확인하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // AI 자동 제어 활성화/비활성화 토글
            aiAutoControlEnabled = !aiAutoControlEnabled;
            
            if (aiAutoControlEnabled)
            {
                btnAutoControl.BackColor = Color.LightGreen;
                btnAutoControl.Text = "AI 자동제어 ON";
                Log("🤖 AI 자동 제어 활성화: 최적값 내에서는 모니터링만 계속하며, 외부 요인으로 값이 벗어나면 실시간으로 자동 조정합니다.");
                // 즉시 한 번 실행 (현재 상태 확인 및 필요시 조정)
                Log("🤖 AI 자동 제어 시작...");
                Task.Run(() => ExecuteAIAutoControl());
            }
            else
            {
                btnAutoControl.BackColor = Color.LightBlue;
                btnAutoControl.Text = "AI 자동제어";
                Log("⏸️ AI 자동 제어 비활성화: 수동 제어만 가능합니다.");
            }
        }
        
        private async void ExecuteAIAutoControl()
        {
            try
            {
                // 현재 센서 데이터 가져오기
                SensorData currentData;
                lock (sensorDataLock)
                {
                    currentData = currentSensorData;
                }
                
                // 센서 데이터 검증
                if (currentData == null)
                {
                    BeginInvoke(new Action(() =>
                    {
                        Log("⚠️ AI 자동 제어: 센서 데이터가 없습니다. 잠시 후 다시 시도하세요.");
                    }));
                    return;
                }
                
                // 센서 값 범위 검증 (잘못된 값 체크)
                // 압력 센서(채광)는 0도 유효한 값일 수 있으므로 >= 0으로 변경
                bool hasValidData = false;
                if ((currentData.Humidity > 0 && currentData.Humidity <= 100) ||
                    (currentData.Temperature >= -10 && currentData.Temperature <= 50) ||
                    (currentData.Light >= 0 && currentData.Light <= 100) ||
                    (currentData.SoilMoisture > 0 && currentData.SoilMoisture <= 100))
                {
                    hasValidData = true;
                }
                
                // 모든 센서 값이 0이거나 범위를 벗어난 경우
                if (!hasValidData)
                {
                    BeginInvoke(new Action(() =>
                    {
                        Log($"⚠️ AI 자동 제어: 유효한 센서 데이터가 없습니다. 센서 값을 확인하세요.");
                        Log($"   습도: {currentData.Humidity:F1}%, 온도: {currentData.Temperature:F1}℃, 채광: {currentData.Light:F1}%, 토양습도: {currentData.SoilMoisture:F1}%");
                    }));
                    return;
                }
                
                // 개별 센서 값 검증 및 정규화
                // 주의: currentData의 값들은 이미 오프셋이 적용된 값입니다
                // Flask 서버는 이 오프셋이 적용된 값을 기준으로 목표값을 계산합니다
                double humidity = Math.Max(0, Math.Min(100, currentData.Humidity));
                double temperature = Math.Max(-10, Math.Min(50, currentData.Temperature));
                double light = Math.Max(0, Math.Min(100, currentData.Light));
                double soilMoisture = Math.Max(0, Math.Min(100, currentData.SoilMoisture));
                
                // 디버깅: 압력 센서(채광)의 경우 오프셋이 적용된 값 확인
                lock (sensorDataLock)
                {
                    if (currentSensorData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"🔍 AI 제어 요청 - 압력 센서: 오프셋적용값={light:F1}%, 원본값={currentSensorData.RawLight:F1}%, 현재오프셋={sensorOffsets[3]:F1}");
                    }
                }
                
                // Flask 서버에 제어 명령 요청 (검증된 값 사용)
                // sensors 형식으로 전송 (rawValue 포함)
                // 주의: 여기서 전송하는 값은 오프셋이 적용된 값입니다
                string jsonData = $@"{{
                    ""sensor_data"": {{
                        ""sensors"": [
                            {{""name"": ""습도"", ""rawValue"": {humidity.ToString("F1")}, ""value"": ""{humidity.ToString("F1")}%""}},
                            {{""name"": ""온도"", ""rawValue"": {temperature.ToString("F1")}, ""value"": ""{temperature.ToString("F1")}℃""}},
                            {{""name"": ""채광"", ""rawValue"": {light.ToString("F1")}, ""value"": ""{light.ToString("F1")}%""}},
                            {{""name"": ""토양습도"", ""rawValue"": {soilMoisture.ToString("F1")}, ""value"": ""{soilMoisture.ToString("F1")}%""}}
                        ],
                        ""humidity"": {humidity.ToString("F1")},
                        ""temperature"": {temperature.ToString("F1")},
                        ""light"": {light.ToString("F1")},
                        ""soil_moisture"": {soilMoisture.ToString("F1")}
                    }},
                    ""farm_id"": {currentFarm}
                }}";
                
                // 디버깅: Flask 서버로 전송하는 데이터 로그
                BeginInvoke(new Action(() =>
                {
                    Log($"🔍 Flask 서버로 전송: 습도={humidity:F1}%, 온도={temperature:F1}℃, 채광={light:F1}%, 토양습도={soilMoisture:F1}%");
                }));
                
                string responseJson = await RequestAIControlAsync(jsonData);
                
                if (string.IsNullOrEmpty(responseJson))
                {
                    BeginInvoke(new Action(() =>
                    {
                        Log("⚠️ AI 자동 제어: Flask 서버 응답이 없습니다.");
                    }));
                    return;
                }
                
                // 응답 파싱 (에러 처리 강화)
                JObject response = null;
                try
                {
                    response = JObject.Parse(responseJson);
                }
                catch (Exception parseEx)
                {
                    BeginInvoke(new Action(() =>
                    {
                        Log($"⚠️ AI 자동 제어: Flask 서버 응답 파싱 실패: {parseEx.Message}");
                    }));
                    return;
                }
                
                if (response != null && response["success"] != null && 
                    response["success"].Value<bool>())
                {
                    // ML 모델 사용 여부 확인
                    bool mlEnabled = false;
                    if (response["ml_enabled"] != null)
                    {
                        mlEnabled = response["ml_enabled"].Value<bool>();
                    }
                    
                    if (mlEnabled)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Log("🤖 AI 자동 제어: ML 모델을 활용한 분석 완료");
                        }));
                    }
                    
                    var commandsArray = response["commands"] as JArray;
                    List<JObject> commands = null;
                    
                    try
                    {
                        if (commandsArray != null)
                        {
                            commands = commandsArray.ToObject<List<JObject>>();
                            
                            // 디버깅: Flask 서버로부터 받은 명령 로그 (UI 스레드에서 실행)
                            BeginInvoke(new Action(() =>
                            {
                                Log($"🔍 Flask 서버 응답: {commands.Count}개의 제어 명령 수신");
                                foreach (var cmd in commands)
                                {
                                    var cmdIndex = cmd["sensor_index"]?.Value<int>() ?? 0;
                                    var cmdName = cmd["sensor_name"]?.Value<string>() ?? "";
                                    var cmdValue = cmd["current_value"]?.Value<double>() ?? 0;
                                    Log($"  → 명령: 인덱스={cmdIndex}, 이름='{cmdName}', 현재값={cmdValue:F1}");
                                }
                            }));
                        }
                    }
                    catch (Exception cmdEx)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Log($"⚠️ AI 자동 제어: 제어 명령 파싱 실패: {cmdEx.Message}");
                        }));
                        return;
                    }
                    
                    if (commands != null && commands.Count > 0)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Log($"🤖 AI 자동 제어: {commands.Count}개의 제어 명령을 받았습니다.");
                            ApplyAIControlCommands(commands);
                        }));
                    }
                    else
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Log("✅ AI 자동 제어: 현재 상태가 최적입니다. 제어가 필요하지 않습니다.");
                        }));
                    }
                }
                else
                {
                    string error = "알 수 없는 오류";
                    if (response != null)
                    {
                        if (response["error"] != null)
                        {
                            error = response["error"].Value<string>();
                        }
                        else if (response["message"] != null)
                        {
                            error = response["message"].Value<string>();
                        }
                    }
                    BeginInvoke(new Action(() =>
                    {
                        Log($"⚠️ AI 자동 제어 실패: {error}");
                    }));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                {
                    Log($"⚠️ AI 자동 제어 오류: {ex.Message}");
                }));
            }
        }
        
        private List<string> GetCropListFromFlask()
        {
            return flaskApi.GetCropList();
        }
        
        private async Task<Dictionary<string, object>> GetCropInfoFromFlask(string cropName)
        {
            try
            {
                return await flaskApi.GetCropInfoAsync(cropName);
            }
            catch (WebException webEx)
            {
                if (webEx.Response is HttpWebResponse httpResponse)
                {
                    try
                    {
                        using (var stream = httpResponse.GetResponseStream())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string errorResponse = await reader.ReadToEndAsync();
                            var errorResult = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(errorResponse);
                            Log($"⚠️ Flask 서버 에러 응답: HTTP {httpResponse.StatusCode}");
                            if (errorResult != null && errorResult.ContainsKey("error"))
                            {
                                Log($"   오류: {errorResult["error"]}");
                            }
                            return errorResult;
                        }
                    }
                    catch { }
                }
                Log($"⚠️ Flask 서버 연결 오류: {webEx.Message}");
            }
            catch (Exception ex)
            {
                Log($"⚠️ 작물 정보 가져오기 오류: {ex.Message}");
            }
            return null;
        }
        
        private string GenerateCropDetailedInfo(string cropName, Dictionary<string, object> cropInfo)
        {
            return FarmController.GenerateCropDetailedInfo(cropName, cropInfo);
        }
        
        private void ApplyCropConditions(int farmId, Dictionary<string, object> cropInfo)
        {
            try
            {
                var thresholds = FarmController.ParseCropThresholds(cropInfo);
                if (thresholds == null) return;

                farmSettings[farmId] = thresholds;
                SaveSensorThresholds();

                Action updateUI = () =>
                {
                    if (currentFarm == farmId)
                    {
                        ApplyFarmSettingsToUI(farmId);
                        Log($"✅ 스마트팜 {farmId}의 TrackBar가 업데이트되었습니다.");
                    }
                    else
                    {
                        Log($"ℹ️ 스마트팜 {farmId}의 설정이 저장되었습니다. (현재 선택: 스마트팜 {currentFarm})");
                    }
                };

                if (InvokeRequired)
                    BeginInvoke(updateUI);
                else
                    updateUI();

                Log($"   습도: {thresholds[0].Min}-{thresholds[0].Max}%, " +
                    $"온도: {thresholds[1].Min}-{thresholds[1].Max}℃, " +
                    $"채광: {thresholds[2].Min}-{thresholds[2].Max}%, " +
                    $"토양습도: {thresholds[3].Min}-{thresholds[3].Max}%");
            }
            catch (Exception ex)
            {
                Log($"⚠️ 작물 조건 적용 오류: {ex.Message}");
            }
        }
        
        private async Task<string> RequestAIControlAsync(string jsonData)
        {
            try
            {
                return await flaskApi.RequestAIControlAsync(jsonData);
            }
            catch (WebException webEx)
            {
                string errorMessage = webEx.Message;
                if (webEx.Response is HttpWebResponse httpResponse)
                {
                    try
                    {
                        using (var stream = httpResponse.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            string errorResponse = await reader.ReadToEndAsync();
                            try
                            {
                                var errorObj = JObject.Parse(errorResponse);
                                if (errorObj["error"] != null)
                                    errorMessage = errorObj["error"].Value<string>();
                            }
                            catch
                            {
                                errorMessage = $"HTTP {httpResponse.StatusCode}: {errorResponse}";
                            }
                        }
                    }
                    catch
                    {
                        errorMessage = $"HTTP {httpResponse.StatusCode}";
                    }
                }

                BeginInvoke(new Action(() => Log($"⚠️ AI 제어 요청 오류: {errorMessage}")));
                return null;
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => Log($"⚠️ AI 제어 요청 오류: {ex.Message}")));
                return null;
            }
        }
        
        private void ApplyAIControlCommands(List<JObject> commands)
        {
            foreach (var command in commands)
            {
                try
                {
                    int sensorIndex = command["sensor_index"]?.Value<int>() ?? 0;
                    string action = command["action"]?.Value<string>() ?? "";
                    double offset = command["offset"]?.Value<double>() ?? 0.0;
                    string originalSensorName = command["sensor_name"] != null 
                        ? command["sensor_name"].Value<string>() 
                        : "";
                    string reason = command["reason"] != null
                        ? command["reason"].Value<string>() 
                        : "";
                    string device = command["device"] != null
                        ? command["device"].Value<string>() 
                        : "";
                    
                    // 센서 이름을 우선 확인하여 올바른 인덱스로 매핑 (Flask 서버가 잘못된 인덱스를 보낼 수 있음)
                    // 센서 매핑: 습도(습도)=1, 온도(온도)=2, 채광(압력)=3, 토양습도(진동)=4
                    // 주의: 더 구체적인 이름을 먼저 체크해야 함 (예: "토양습도"를 "습도"보다 먼저)
                    int originalSensorIndex = sensorIndex;
                    bool sensorNameMatched = false;
                    string sensorName = "";
                    
                    // 원본 센서 이름이 있으면 먼저 이름으로 인덱스 결정
                    if (!string.IsNullOrEmpty(originalSensorName))
                    {
                        // 센서 이름으로 인덱스 찾기 (더 구체적인 이름을 먼저 체크)
                        // 1. 토양습도 (토양, soil, 진동, vibration 포함) - 인덱스 4
                        if (originalSensorName.Contains("토양") || originalSensorName.Contains("soil") || originalSensorName.Contains("진동") || originalSensorName.Contains("vibration"))
                        {
                            sensorIndex = 4; // 토양습도 센서
                            sensorName = "토양습도";
                            sensorNameMatched = true;
                        }
                        // 2. 채광/압력 - 인덱스 3
                        else if (originalSensorName.Contains("채광") || originalSensorName.Contains("압력") || originalSensorName.Contains("light") || originalSensorName.Contains("pressure"))
                        {
                            sensorIndex = 3; // 채광/압력 센서
                            sensorName = "채광";
                            sensorNameMatched = true;
                        }
                        // 3. 온도 - 인덱스 2
                        else if (originalSensorName.Contains("온도") || originalSensorName.Contains("temperature"))
                        {
                            sensorIndex = 2;
                            sensorName = "온도";
                            sensorNameMatched = true;
                        }
                        // 4. 습도 (일반 습도, 토양습도가 아닌 경우) - 인덱스 1
                        else if (originalSensorName.Contains("습도") || originalSensorName.Contains("humidity"))
                        {
                            sensorIndex = 1;
                            sensorName = "습도";
                            sensorNameMatched = true;
                        }
                    }
                    
                    // 센서 이름이 없거나 매칭되지 않았으면 센서 인덱스로 판단
                    if (!sensorNameMatched)
                    {
                        if (sensorIndex >= 1 && sensorIndex <= SensorCount)
                        {
                            // 센서 인덱스로부터 이름 가져오기
                            sensorName = sensorDisplayNames[sensorIndex];
                            
                            // 센서 인덱스별 고정 매핑 확인
                            // 인덱스 1=습도, 2=온도, 3=채광, 4=토양습도
                            if (sensorIndex == 1)
                            {
                                sensorName = "습도";
                            }
                            else if (sensorIndex == 2)
                            {
                                sensorName = "온도";
                            }
                            else if (sensorIndex == 3)
                            {
                                sensorName = "채광";
                            }
                            else if (sensorIndex == 4)
                            {
                                sensorName = "토양습도";
                            }
                        }
                        else
                        {
                            sensorName = "알 수 없음";
                        }
                    }
                    
                    // 센서 인덱스 유효성 검증
                    if (sensorIndex < 1 || sensorIndex > SensorCount)
                    {
                        Log($"⚠️ AI 자동 제어: 잘못된 센서 인덱스 {originalSensorIndex}, 센서 이름: {sensorName} (유효 범위: 1-{SensorCount})");
                        continue;
                    }
                    
                    // 센서 이름과 인덱스가 일치하지 않으면 경고 및 수정 (하지만 계속 진행)
                    if (originalSensorIndex != sensorIndex)
                    {
                        if (originalSensorIndex >= 1 && originalSensorIndex <= SensorCount)
                        {
                            Log($"ℹ️ AI 자동 제어: 센서 이름 '{originalSensorName}'과 인덱스 {originalSensorIndex}가 일치하지 않습니다. 센서 이름 기준으로 인덱스 {sensorIndex}로 수정하여 계속 진행합니다.");
                        }
                        else
                        {
                            Log($"ℹ️ AI 자동 제어: 센서 이름 '{originalSensorName}'을 인덱스 {sensorIndex}로 매핑했습니다.");
                        }
                    }
                    
                    // 디버깅: 센서 인덱스와 이름 확인 (항상 로그 출력)
                    // Flask 서버가 보낸 원본 데이터와 최종 매핑 결과를 모두 표시
                    Log($"🔍 AI 제어 명령 상세: 원본이름='{originalSensorName}', 최종이름='{sensorName}', 원본인덱스={originalSensorIndex}, 최종인덱스={sensorIndex}, 액션={action}, 이름매칭={sensorNameMatched}");
                    
                    // 센서 이름과 인덱스 불일치 시 경고 (특히 압력/채광 센서)
                    if (originalSensorIndex == 1 && (originalSensorName.Contains("압력") || originalSensorName.Contains("채광") || originalSensorName.Contains("pressure") || originalSensorName.Contains("light")))
                    {
                        Log($"⚠️ 중요: Flask 서버가 인덱스 1(습도)로 '압력/채광' 센서를 보냈습니다. 인덱스 {sensorIndex}(채광)로 수정합니다.");
                    }
                    
                    // 센서 인덱스가 3인데 센서 이름이 "습도"인 경우도 체크 (Flask 서버 버그 대비)
                    if (originalSensorIndex == 3 && !string.IsNullOrEmpty(originalSensorName) && 
                        (originalSensorName.Contains("습도") || originalSensorName.Contains("humidity")) && 
                        !originalSensorName.Contains("토양"))
                    {
                        Log($"⚠️ 중요: Flask 서버가 인덱스 3(채광)으로 '습도' 센서를 보냈습니다. 센서 이름 '{originalSensorName}'을 확인하세요.");
                        // 센서 이름이 "습도"이면 인덱스 1로 수정
                        if (originalSensorName.Contains("습도") && !originalSensorName.Contains("토양"))
                        {
                            sensorIndex = 1;
                            sensorName = "습도";
                            Log($"⚠️ 센서 이름이 '습도'이므로 인덱스를 1로 수정합니다.");
                        }
                    }
                    
                    // ML 모델 정보 확인
                    bool? mlAnomaly = null;
                    double? mlConfidence = null;
                    if (command["ml_anomaly"] != null && command["ml_anomaly"].Type != JTokenType.Null)
                    {
                        mlAnomaly = command["ml_anomaly"].Value<bool>();
                    }
                    if (command["ml_confidence"] != null && command["ml_confidence"].Type != JTokenType.Null)
                    {
                        mlConfidence = command["ml_confidence"].Value<double>();
                    }
                    
                    // 오프셋 적용 (절대값 방식: 최적값까지 바로 맞추기)
                    // 오프셋이 적용된 현재 값을 기준으로 목표값까지 필요한 오프셋을 계산
                    // 원본 센서 값을 기준으로 오프셋을 설정하여 안정적으로 제어
                    lock (sensorDataLock)
                    {
                        // 오프셋이 적용된 현재 센서 값 (AI 제어 시 이 값을 기준으로 계산)
                        double currentSensorValueWithOffset = 0.0;
                        // 원본 센서 값 (오프셋 계산 시 사용)
                        double rawSensorValue = 0.0;
                        
                        if (currentSensorData != null)
                        {
                            switch (sensorIndex)
                            {
                                case 1: 
                                    currentSensorValueWithOffset = currentSensorData.Humidity; 
                                    rawSensorValue = currentSensorData.RawHumidity; 
                                    break;
                                case 2: 
                                    currentSensorValueWithOffset = currentSensorData.Temperature; 
                                    rawSensorValue = currentSensorData.RawTemperature; 
                                    break;
                                case 3: 
                                    currentSensorValueWithOffset = currentSensorData.Light; 
                                    rawSensorValue = currentSensorData.RawLight; 
                                    break;
                                case 4: 
                                    currentSensorValueWithOffset = currentSensorData.SoilMoisture; 
                                    rawSensorValue = currentSensorData.RawSoilMoisture; 
                                    break;
                                default:
                                    Log($"⚠️ AI 자동 제어: 알 수 없는 센서 인덱스 {sensorIndex}");
                                    continue;
                            }
                        }
                        else
                        {
                            Log($"⚠️ AI 자동 제어: 센서 데이터가 없습니다. (센서: {sensorName})");
                            continue;
                        }
                        
                        // 센서 인덱스를 thresholds 배열 인덱스로 변환 (1→0, 2→1, 3→2, 4→3)
                        int thresholdIndex = sensorIndex - 1;
                        
                        // 현재 농장의 센서 임계값 가져오기 (Min, Max)
                        int minThreshold = 0;
                        int maxThreshold = 100;
                        if (farmSettings.ContainsKey(currentFarm) && 
                            farmSettings[currentFarm] != null && 
                            thresholdIndex >= 0 && thresholdIndex < SensorCount)
                        {
                            var threshold = farmSettings[currentFarm][thresholdIndex];
                            minThreshold = threshold.Min;
                            maxThreshold = threshold.Max;
                        }
                        
                        // 목표값 가져오기
                        double? targetValue = command["target_value"]?.Value<double>();
                        // 채광(압력) 센서는 0도 유효한 값이므로 >= 0으로 변경
                        // 다른 센서도 0이 될 수 있으므로 >= 0으로 변경
                        if (targetValue.HasValue && rawSensorValue >= 0)
                        {
                            // 목표값을 Min과 Max 범위 내로 제한
                            double clampedTargetValue = Math.Max(minThreshold, Math.Min(maxThreshold, targetValue.Value));
                            
                            // Flask 서버는 오프셋이 적용된 현재값(currentSensorValueWithOffset)을 기준으로 목표값을 계산합니다.
                            // 따라서 목표값은 오프셋이 적용된 값을 기준으로 한 것입니다.
                            // 오프셋을 원본 센서 값에 적용해야 하므로:
                            // 목표값 = 원본값 + 새로운오프셋
                            // 새로운오프셋 = 목표값 - 원본값
                            double offsetNeeded = clampedTargetValue - rawSensorValue;
                            
                            // 디버깅: 압력 센서(채광)의 경우 상세 로그
                            if (sensorIndex == 3)
                            {
                                Log($"🔍 압력 센서 오프셋 계산 상세: 목표={clampedTargetValue:F1}%, 오프셋적용현재={currentSensorValueWithOffset:F1}%, 원본={rawSensorValue:F1}%, 현재오프셋={sensorOffsets[3]:F1}, 계산된오프셋={offsetNeeded:F1}");
                            }
                            
                            // 오프셋을 적용한 최종 값이 범위 내에 있는지 확인
                            double finalValue = rawSensorValue + offsetNeeded;
                            if (finalValue < minThreshold)
                            {
                                // 최소값보다 작으면 최소값으로 조정
                                offsetNeeded = minThreshold - rawSensorValue;
                                clampedTargetValue = minThreshold;
                            }
                            else if (finalValue > maxThreshold)
                            {
                                // 최대값보다 크면 최대값으로 조정
                                offsetNeeded = maxThreshold - rawSensorValue;
                                clampedTargetValue = maxThreshold;
                            }
                            
                            // 오프셋 설정 전 이전 값 확인 (압력 센서 디버깅)
                            double previousOffset = sensorOffsets[sensorIndex];
                            sensorOffsets[sensorIndex] = offsetNeeded;  // 누적이 아닌 절대값 설정
                            
                            // 압력 센서의 경우 오프셋 변경 확인
                            if (sensorIndex == 3 && Math.Abs(previousOffset - offsetNeeded) > 0.1)
                            {
                                Log($"🔧 압력 센서 오프셋 변경: {previousOffset:F1} → {offsetNeeded:F1}");
                            }
                            
                            // 현재 농장의 오프셋도 저장 (농장별 오프셋 독립 관리)
                            if (!farmOffsets.ContainsKey(currentFarm))
                            {
                                farmOffsets[currentFarm] = new double[SensorCount + 1];
                            }
                            farmOffsets[currentFarm][sensorIndex] = offsetNeeded;
                            
                            string offsetStr = offsetNeeded >= 0 ? $"+{offsetNeeded:F1}" : $"{offsetNeeded:F1}";
                            
                            // 로그 메시지 생성 (ML 정보 포함)
                            // 오프셋이 적용된 현재값과 원본값을 모두 표시
                            string logMessage = $"🤖 AI 자동 제어: {sensorName} {device} 작동 → 오프셋 {offsetStr} (목표: {clampedTargetValue:F1}, 현재(오프셋적용): {currentSensorValueWithOffset:F1}, 원본: {rawSensorValue:F1}, 범위: {minThreshold}-{maxThreshold})";
                            
                            // 원래 목표값이 범위를 벗어났다면 경고
                            if (targetValue.Value != clampedTargetValue)
                            {
                                logMessage += $" [범위 조정: 원래 목표값 {targetValue.Value:F1} → {clampedTargetValue:F1}]";
                            }
                            
                            if (mlAnomaly.HasValue && mlConfidence.HasValue)
                            {
                                if (mlAnomaly.Value)
                                {
                                    logMessage += $" [ML 이상 징후 감지, 신뢰도: {mlConfidence.Value:P1}]";
                                }
                                else
                                {
                                    logMessage += $" [ML 정상 예측, 신뢰도: {mlConfidence.Value:P1}]";
                                }
                            }
                            
                            Log(logMessage);
                        }
                        else
                        {
                            // 목표값이 없으면 기존 방식 (누적)
                            // 원본 센서 값을 기준으로 오프셋 계산
                            // 채광(압력) 센서는 0도 유효한 값이므로 >= 0 체크
                            if (rawSensorValue < 0)
                            {
                                Log($"⚠️ AI 자동 제어: {sensorName} 원본 센서 값이 유효하지 않습니다. (값: {rawSensorValue:F1})");
                                continue;
                            }
                            
                            string offsetStr = "";
                            double newOffset = sensorOffsets[sensorIndex];
                            
                            if (action == "increase")
                            {
                                newOffset = sensorOffsets[sensorIndex] + offset;
                                offsetStr = $"+{offset:F1}";
                            }
                            else if (action == "decrease")
                            {
                                newOffset = sensorOffsets[sensorIndex] - offset;
                                offsetStr = $"-{offset:F1}";
                            }
                            else
                            {
                                Log($"⚠️ AI 자동 제어: {sensorName} 알 수 없는 액션: {action}");
                                continue;
                            }
                            
                            // 원본 센서 값을 기준으로 오프셋을 적용한 최종 값이 범위 내에 있는지 확인
                            double finalValue = rawSensorValue + newOffset;
                            if (finalValue < minThreshold)
                            {
                                // 최소값보다 작으면 최소값으로 조정
                                newOffset = minThreshold - rawSensorValue;
                                offsetStr = $"조정됨 (범위: {minThreshold}-{maxThreshold})";
                            }
                            else if (finalValue > maxThreshold)
                            {
                                // 최대값보다 크면 최대값으로 조정
                                newOffset = maxThreshold - rawSensorValue;
                                offsetStr = $"조정됨 (범위: {minThreshold}-{maxThreshold})";
                            }
                            
                            sensorOffsets[sensorIndex] = newOffset;
                            
                            // 현재 농장의 오프셋도 저장 (농장별 오프셋 독립 관리)
                            if (!farmOffsets.ContainsKey(currentFarm))
                            {
                                farmOffsets[currentFarm] = new double[SensorCount + 1];
                            }
                            farmOffsets[currentFarm][sensorIndex] = newOffset;
                            
                            // 로그 메시지 생성 (ML 정보 포함)
                            // 오프셋이 적용된 현재값과 원본값을 모두 표시
                            string logMessage = $"🤖 AI 자동 제어: {sensorName} {device} 작동 → 오프셋 {offsetStr} (현재 오프셋: {newOffset:F1}, 현재(오프셋적용): {currentSensorValueWithOffset:F1}, 원본: {rawSensorValue:F1}, 범위: {minThreshold}-{maxThreshold})";
                            
                            if (mlAnomaly.HasValue && mlConfidence.HasValue)
                            {
                                if (mlAnomaly.Value)
                                {
                                    logMessage += $" [ML 이상 징후 감지, 신뢰도: {mlConfidence.Value:P1}]";
                                }
                                else
                                {
                                    logMessage += $" [ML 정상 예측, 신뢰도: {mlConfidence.Value:P1}]";
                                }
                            }
                            
                            Log(logMessage);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(reason))
                    {
                        Log($"   → {reason}");
                    }
                    
                    // UI 즉시 업데이트 (현재 센서 데이터 기반으로 UI 갱신)
                    if (adsConnected && powerOn)
                    {
                        lock (sensorDataLock)
                        {
                            if (currentSensorData != null)
                            {
                                // 현재 센서 데이터를 기반으로 UI 업데이트
                                var bar1 = FindSensorControl<ProgressBar>("barSensor1");
                                var bar2 = FindSensorControl<ProgressBar>("barSensor2");
                                var bar3 = FindSensorControl<ProgressBar>("barSensor3");
                                var bar4 = FindSensorControl<ProgressBar>("barSensor4");
                                
                                if (bar1 != null) bar1.Value = ClampToProgress(currentSensorData.Humidity);
                                // 온도는 다른 범위 사용 (10-40℃ → ProgressBar 값)
                                int temperatureBarValue = (int)Math.Round(Clamp(currentSensorData.Temperature, 10, 40));
                                if (bar2 != null) bar2.Value = temperatureBarValue;
                                if (bar3 != null) bar3.Value = ClampToProgress(currentSensorData.Light);
                                if (bar4 != null) bar4.Value = ClampToProgress(currentSensorData.SoilMoisture);
                                
                                var lbl1 = FindSensorControl<Label>("lblSensorValue1");
                                var lbl2 = FindSensorControl<Label>("lblSensorValue2");
                                var lbl3 = FindSensorControl<Label>("lblSensorValue3");
                                var lbl4 = FindSensorControl<Label>("lblSensorValue4");
                                
                                if (lbl1 != null) lbl1.Text = $"값: {currentSensorData.Humidity:F1}%";
                                if (lbl2 != null) lbl2.Text = $"값: {currentSensorData.Temperature:F1}℃";
                                if (lbl3 != null) lbl3.Text = $"값: {currentSensorData.Light:F1}%";
                                if (lbl4 != null) lbl4.Text = $"값: {currentSensorData.SoilMoisture:F1}%";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"⚠️ 제어 명령 적용 오류 (센서 인덱스: {command["sensor_index"]?.Value<int>() ?? 0}): {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"제어 명령 적용 오류 상세: {ex}");
                }
            }
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            // 폼이 완전히 로드된 후 UI 요소 초기화
            InitializeAutoControlUI();
        }
        
        // 센서 임계값 저장
        private void SaveSensorThresholds()
        {
            try
            {
                settingsRepo.SaveThresholds(farmSettings);
            }
            catch (Exception ex)
            {
                Log($"⚠️ 센서 임계값 저장 실패: {ex.Message}");
            }
        }
        
        private void LoadSensorThresholds()
        {
            try
            {
                var loaded = settingsRepo.LoadThresholds();
                foreach (var kvp in loaded)
                {
                    farmSettings[kvp.Key] = kvp.Value;
                    if (kvp.Key == currentFarm)
                        ApplyFarmSettingsToUI(kvp.Key);
                }
                if (loaded.Count > 0)
                    Log("✅ 저장된 센서 임계값 불러오기 완료");
            }
            catch (Exception ex)
            {
                Log($"⚠️ 센서 임계값 불러오기 실패: {ex.Message}");
            }
        }
        
        private void SaveFarmInfo()
        {
            try
            {
                settingsRepo.SaveFarmInfo(farmCropNames, farmNotes);
            }
            catch (Exception ex)
            {
                Log($"⚠️ 스마트팜 정보 저장 실패: {ex.Message}");
            }
        }
        
        private void LoadFarmInfo()
        {
            try
            {
                settingsRepo.LoadFarmInfo(farmCropNames, farmNotes);
                Log("✅ 저장된 스마트팜 정보 불러오기 완료");
            }
            catch (Exception ex)
            {
                Log($"⚠️ 스마트팜 정보 불러오기 실패: {ex.Message}");
            }
        }
        
        // SensorData moved to Models/SensorData.cs

        private void InitializeSensorUI()
        {
            panelMain.Controls.Clear();

            FlowLayoutPanel flowSensors = new FlowLayoutPanel
            {
                Name = "flowSensors",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.WhiteSmoke
            };

            panelMain.Controls.Add(flowSensors);

            string[] sensorNames = { "습도 (습도)", "온도 (온도)", "채광 (압력)", "토양습도 (진동)" };

            for (int i = 0; i < sensorNames.Length; i++)
            {
                int index = i + 1;

                Panel sensorPanel = new Panel
                {
                    Name = $"sensorPanel{index}",
                    Size = new Size(620, 190),
                    Margin = new Padding(0, 0, 0, 15),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblSensor = new Label
                {
                    Text = sensorNames[i],
                    Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(10, 10)
                };

                ProgressBar bar = new ProgressBar
                {
                    Name = $"barSensor{index}",
                    Size = new Size(320, 25),
                    Location = new Point(150, 10),
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };

                Panel lamp = new Panel
                {
                    Name = $"lampSensor{index}",
                    Size = new Size(25, 25),
                    Location = new Point(490, 10),
                    BackColor = Color.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblValue = new Label
                {
                    Name = $"lblSensorValue{index}",
                    Text = "값: -",
                    Font = new Font("맑은 고딕", 9),
                    AutoSize = true,
                    Location = new Point(530, 14)
                };

                Button btnEditValue = new Button
                {
                    Name = $"btnEditSensorValue{index}",
                    Text = "값 수정",
                    Font = new Font("맑은 고딕", 8),
                    Size = new Size(70, 25),
                    Location = new Point(530, 40),
                    UseVisualStyleBackColor = true
                };
                btnEditValue.Click += (s, e) => HandleEditSensorValue(index);

                Label lblMin = new Label
                {
                    Name = $"lblSensorMinValue{index}",
                    Text = "최소값: 30",
                    Font = new Font("맑은 고딕", 9),
                    AutoSize = true,
                    Location = new Point(150, 50)
                };

                TrackBar trackMin = new TrackBar
                {
                    Name = $"trackSensor{index}Min",
                    Minimum = 0,
                    Maximum = 100,
                    Value = 30,
                    TickFrequency = 5,
                    Size = new Size(320, 45),
                    Location = new Point(150, 70)
                };

                Label lblMax = new Label
                {
                    Name = $"lblSensorMaxValue{index}",
                    Text = "최대값: 70",
                    Font = new Font("맑은 고딕", 9),
                    AutoSize = true,
                    Location = new Point(150, 120)
                };

                TrackBar trackMax = new TrackBar
                {
                    Name = $"trackSensor{index}Max",
                    Minimum = 0,
                    Maximum = 100,
                    Value = 70,
                    TickFrequency = 5,
                    Size = new Size(320, 45),
                    Location = new Point(150, 140)
                };

                if (index == 2)
                {
                    bar.Minimum = 10;
                    bar.Maximum = 40;
                    bar.Value = 10;

                    trackMin.Minimum = 10;
                    trackMin.Maximum = 40;
                    trackMin.Value = 10;
                    trackMin.TickFrequency = 1;

                    trackMax.Minimum = 10;
                    trackMax.Maximum = 40;
                    trackMax.Value = 40;
                    trackMax.TickFrequency = 1;

                    lblMin.Text = "최소값: 10도";
                    lblMax.Text = "최대값: 40도";
                }

                trackMin.Scroll += (s, e) => HandleThresholdChanged(index, true);
                trackMax.Scroll += (s, e) => HandleThresholdChanged(index, false);

                sensorPanel.Controls.Add(lblSensor);
                sensorPanel.Controls.Add(bar);
                sensorPanel.Controls.Add(lamp);
                sensorPanel.Controls.Add(lblValue);
                sensorPanel.Controls.Add(btnEditValue);
                sensorPanel.Controls.Add(lblMin);
                sensorPanel.Controls.Add(trackMin);
                sensorPanel.Controls.Add(lblMax);
                sensorPanel.Controls.Add(trackMax);

                flowSensors.Controls.Add(sensorPanel);
            }
        }

        private void InitializeFarmSettings()
        {
            for (int farm = 1; farm <= 3; farm++)
            {
                var thresholds = new SensorThreshold[SensorCount];
                
                if (farm == 1)
                {
                    // 스마트팜 1: 사과 재배 최적 환경 설정
                    // 센서 인덱스: 0=습도, 1=온도, 2=채광, 3=토양습도
                    thresholds[0] = new SensorThreshold { Min = 30, Max = 80 }; // 습도: 30-80%
                    thresholds[1] = new SensorThreshold { Min = 0, Max = 35 };  // 온도: 0-35℃ (TrackBar는 0-100이지만 실제 온도 범위)
                    thresholds[2] = new SensorThreshold { Min = 50, Max = 80 }; // 채광: 50-80% (충분한 햇빛 필요)
                    thresholds[3] = new SensorThreshold { Min = 20, Max = 60 }; // 토양습도: 20-60%
                    
                    // 스마트팜 1번 기본 정보 설정 (사과 재배 환경)
                    if (!farmCropNames.ContainsKey(1))
                    {
                        farmCropNames[1] = "사과";
                    }
                    if (!farmNotes.ContainsKey(1))
                    {
                        farmNotes[1] = "【 사과 재배 최적 환경 】\n\n" +
                                      "• 습도: 30-80% (생육기 60-80%, 수확기 50-70%)\n" +
                                      "  - 30% 이하: 증산 작용 활발, 수분 스트레스 발생\n" +
                                      "  - 80% 이상: 병해 발생 위험 증가\n\n" +
                                      "• 온도: 0-35℃ (생육기 15-25℃, 저장 0-5℃)\n" +
                                      "  - 0℃ 이하: 동해 발생 가능\n" +
                                      "  - 35℃ 이상: 생육 저하, 열 스트레스\n\n" +
                                      "• 채광: 50-80% (충분한 일조량 필요)\n" +
                                      "  - 광합성 활성화, 과실 당도 향상\n" +
                                      "  - 과실 착색 및 품질 개선\n\n" +
                                      "• 토양습도: 20-60% (적정 수분 유지)\n" +
                                      "  - 20% 이하: 뿌리 수분 흡수 어려움\n" +
                                      "  - 60% 이상: 뿌리 부패 위험\n\n" +
                                      "※ 위 환경 조건을 유지하면 사과의 품질과 수확량이 향상됩니다.";
                    }
                }
                else
                {
                    // 스마트팜 2, 3: 기본값
                    for (int sensor = 0; sensor < SensorCount; sensor++)
                    {
                        bool isTemperature = (sensor == 1);
                        thresholds[sensor] = new SensorThreshold
                        {
                            Min = isTemperature ? 10 : 30,
                            Max = isTemperature ? 40 : 70
                        };
                    }
                }
                
                farmSettings[farm] = thresholds;
            }

            ApplyFarmSettingsToUI(currentFarm);
        }

        private void ApplyFarmSettingsToUI(int farmId)
        {
            if (!farmSettings.TryGetValue(farmId, out var thresholds))
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ ApplyFarmSettingsToUI: 농장 {farmId}의 설정을 찾을 수 없습니다.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ ApplyFarmSettingsToUI: 농장 {farmId}의 설정을 UI에 적용합니다.");

            for (int sensor = 0; sensor < thresholds.Length && sensor < SensorCount; sensor++)
            {
                int index = sensor + 1;
                var (minTrack, maxTrack, lblMin, lblMax) = GetThresholdControls(index);
                
                if (minTrack == null || maxTrack == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ ApplyFarmSettingsToUI: 센서 {index}의 TrackBar를 찾을 수 없습니다.");
                    continue;
                }
                
                // 온도 센서(index=2)의 경우 TrackBar 범위가 10-40이지만 실제 온도는 0-35℃ 범위
                if (index == 2) // 온도 센서
                {
                    // 온도 TrackBar는 10-40 범위를 사용하지만, 실제 온도는 0-35℃
                    // 임계값을 TrackBar 범위(10-40)에 맞게 조정
                    int oldMin = minTrack.Value;
                    int oldMax = maxTrack.Value;
                    
                    int trackMin = Math.Max(10, Math.Min(40, thresholds[sensor].Min));
                    int trackMax = Math.Max(10, Math.Min(40, thresholds[sensor].Max));
                    
                    minTrack.Value = Math.Max(minTrack.Minimum, Math.Min(minTrack.Maximum, trackMin));
                    maxTrack.Value = Math.Max(maxTrack.Minimum, Math.Min(maxTrack.Maximum, trackMax));
                    
                    System.Diagnostics.Debug.WriteLine($"   온도 TrackBar 업데이트: {oldMin}-{oldMax}℃ → {minTrack.Value}-{maxTrack.Value}℃ (임계값: {thresholds[sensor].Min}-{thresholds[sensor].Max}℃, TrackBar 값: {trackMin}-{trackMax})");
                }
                else
                {
                    // 다른 센서들은 0-100 범위
                    int oldMin = minTrack.Value;
                    int oldMax = maxTrack.Value;
                    
                    int minValue = Math.Max(0, Math.Min(100, thresholds[sensor].Min));
                    int maxValue = Math.Max(0, Math.Min(100, thresholds[sensor].Max));
                    
                    minTrack.Value = Math.Max(minTrack.Minimum, Math.Min(minTrack.Maximum, minValue));
                    maxTrack.Value = Math.Max(maxTrack.Minimum, Math.Min(maxTrack.Maximum, maxValue));
                    
                    System.Diagnostics.Debug.WriteLine($"   센서 {index} TrackBar 업데이트: {oldMin}-{oldMax}% → {minTrack.Value}-{maxTrack.Value}% (임계값: {thresholds[sensor].Min}-{thresholds[sensor].Max}%)");
                }
                
                // 레이블 업데이트
                UpdateThresholdLabels(index, thresholds[sensor].Min, thresholds[sensor].Max);
                
                // TrackBar 새로고침 (변경사항 즉시 반영)
                minTrack.Refresh();
                maxTrack.Refresh();
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ ApplyFarmSettingsToUI: 농장 {farmId}의 UI 업데이트 완료");
        }


        private void btnPower_Click(object sender, EventArgs e)
        {
            // 전원 버튼은 양방향 통신 (UI에서도 제어 가능, 장비에서도 제어 가능)
            powerOn = !powerOn;
            if (powerOn)
            {
                btnPower.Text = "전원 ON";
                btnPower.BackColor = Color.LightGreen;
                lblConnection.Text = "전원 상태: ON";
                lblConnection.ForeColor = Color.Green;
                SetOperationalControlsEnabled(true);
                UpdateFarmButtonStyles();
                Log("전원 켜짐 (UI 버튼)");
            }
            else
            {
                btnPower.Text = "전원 OFF";
                btnPower.BackColor = Color.LightGray;
                lblConnection.Text = "전원 상태: OFF";
                lblConnection.ForeColor = Color.Red;
                SetOperationalControlsEnabled(false);
                ethercatPowerOn = false;
                DisconnectFromPlc();
                SetEthercatStatus(EthercatConnectionStatus.Off);
                UpdateFarmButtonStyles();
                ResetSensorVisuals();
                Log("전원 꺼짐 (UI 버튼)");
            }
            UpdateDigitalOutputs();
        }

        private void BtnFarm_Click(object sender, EventArgs e)
        {
            if (!powerOn) { Log("⚠️ 전원 OFF 상태에서는 스마트팜 전환 불가"); return; }
            Button btn = sender as Button;
            int newFarmId = (int)btn.Tag;
            
            // 현재 농장의 오프셋 저장 (농장별 오프셋 독립 관리)
            if (!farmOffsets.ContainsKey(currentFarm))
            {
                farmOffsets[currentFarm] = new double[SensorCount + 1];
            }
            Array.Copy(sensorOffsets, farmOffsets[currentFarm], SensorCount + 1);
            
            // 새 농장으로 변경
            currentFarm = newFarmId;
            
            // 새 농장의 오프셋 복원 (없으면 초기화)
            if (farmOffsets.ContainsKey(currentFarm))
            {
                Array.Copy(farmOffsets[currentFarm], sensorOffsets, SensorCount + 1);
                Log($"ℹ️ 스마트팜 {currentFarm}번의 저장된 오프셋을 복원했습니다.");
            }
            else
            {
                // 새 농장이면 오프셋 초기화
                for (int i = 0; i <= SensorCount; i++)
                {
                    sensorOffsets[i] = 0.0;
                }
                Log($"ℹ️ 스마트팜 {currentFarm}번의 오프셋을 초기화했습니다.");
            }
            
            ApplyFarmSettingsToUI(currentFarm);
            UpdateFarmButtonStyles();
            ResetSensorAlerts();
            Log($"스마트팜 {currentFarm}번 선택");
            
            // Flask 서버에 농장 변경 알림 (웹 UI와 AI가 최신 농장 정보 사용)
            if (flaskServerRunning)
            {
                SendFarmChangeToFlask();
                
                // AI 자동 제어가 활성화되어 있으면 작물 변경 시 자동으로 새 작물의 최적값에 맞춰 조정
                if (aiAutoControlEnabled && powerOn && adsConnected)
                {
                    // 현재 센서 값 가져오기
                    SensorData currentData;
                    lock (sensorDataLock)
                    {
                        currentData = currentSensorData;
                    }
                    
                    if (currentData != null && 
                        (currentData.Humidity > 0 || currentData.Temperature > -10 || 
                         currentData.Light > 0 || currentData.SoilMoisture > 0))
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Log($"🔄 작물 변경 감지: 스마트팜 {currentFarm}번의 최적값에 맞춰 AI 자동 제어 실행...");
                        }));
                        
                        // 작물 변경 후 바로 AI 제어 실행 (쿨다운 무시)
                        lastAIAutoControlExecution = DateTime.MinValue; // 쿨다운 초기화
                        _ = CheckAndAutoControlIfNeeded(
                            currentData.Humidity, 
                            currentData.Temperature, 
                            currentData.Light, 
                            currentData.SoilMoisture
                        );
                    }
                }
            }
            
            // 재배 작물 및 정보는 로그에 출력하지 않음
            UpdateDigitalOutputs();
        }
        
        private void SendFarmChangeToFlask()
        {
            if (!flaskServerRunning) return;

            Task.Run(() =>
            {
                string farmInfoJson = GetSensorDataJson();
                flaskApi.SendFarmChange(farmInfoJson);
            });
        }

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            if (logForm == null || logForm.IsDisposed)
            {
                logForm = new LogForm
                {
                    GetLogs = () => logService.LogHistory,
                    SetLogs = (logs) =>
                    {
                        logService.ClearLogs();
                        foreach (var l in logs) logService.Log(l);
                        UpdateLogPreview();
                        if (logForm != null && !logForm.IsDisposed)
                            logForm.UpdateLogs(logService.LogHistory);
                    },
                    Log = (msg) => Log(msg)
                };
                logForm.FormClosed += (s, args) => logForm = null;
                PositionLogForm();
                logForm.Show(this);
            }
            else
            {
                PositionLogForm();
                if (!logForm.Visible)
                    logForm.Show(this);
                else
                    logForm.Activate();
            }

            logForm.UpdateLogs(GetLogEntries());
        }

        private void btnWebConnect_Click(object sender, EventArgs e)
        {
            if (!powerOn) 
            { 
                Log("⚠️ 전원 OFF 상태에서는 웹 연결 불가"); 
                return; 
            }
            
            // Flask 서버 연결 확인 및 브라우저 열기
            Task.Run(() =>
            {
                try
                {
                    if (CheckFlaskServerConnection())
                    {
                        BeginInvoke(new Action(() =>
                        {
                            btnWebConnect.Text = "웹 연결 중";
                            btnWebConnect.BackColor = Color.LightGreen;
                            Log($"🌐 Flask 서버 연결 확인: {FlaskServerUrl}");
                            try
                            {
                                System.Diagnostics.Process.Start(FlaskServerUrl);
                            }
                            catch (Exception ex)
                            {
                                Log($"브라우저 열기 실패: {ex.Message}");
                            }
                        }));
                    }
                    else
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Log("⚠️ Flask 서버에 연결할 수 없습니다. 서버가 실행 중인지 확인하세요.");
                            MessageBox.Show(
                                "Flask 서버에 연결할 수 없습니다.\n\n" +
                                "다음 명령어로 Flask 서버를 시작하세요:\n" +
                                "python app.py\n\n" +
                                $"서버 주소: {FlaskServerUrl}",
                                "Flask 서버 연결 실패",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning
                            );
                            btnWebConnect.Text = "웹 연결";
                            btnWebConnect.BackColor = SystemColors.Control;
                        }));
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        Log($"⚠️ Flask 서버 연결 중 오류: {ex.Message}");
                        btnWebConnect.Text = "웹 연결";
                        btnWebConnect.BackColor = SystemColors.Control;
                    }));
                }
            });
        }
        
        // Flask 서버 연결 확인
        private bool CheckFlaskServerConnection()
        {
            return flaskApi.CheckConnection();
        }
        
        private void SendSensorDataToFlask()
        {
            if (!flaskServerRunning) return;

            Task.Run(() =>
            {
                string sensorDataJson = GetSensorDataJson();
                flaskApi.SendSensorData(sensorDataJson);
            });
        }

        private void Log(string message)
        {
            logService.Log(message);
        }

        private void LogWarning(string message)
        {
            logService.LogWarning(message);
        }

        private void LogSensorData(string status = "정상")
        {
            if (!powerOn || !adsConnected) return;

            lock (sensorDataLock)
            {
                logService.LogSensorData(currentSensorData, status);
                lastSensorLogTime = DateTime.Now;
            }
        }

        private void OnLogEntryAdded(string entry)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnLogEntryAdded(entry)));
                return;
            }
            UpdateLogPreview();
            if (logForm != null && !logForm.IsDisposed)
            {
                logForm.UpdateLogs(logService.LogHistory);
            }
        }
        
        // 정기 센서 데이터 로깅 타이머 콜백
        private void SensorLogTimerCallback(object state)
        {
            if (!powerOn || !adsConnected) return;
            
            BeginInvoke(new Action(() =>
            {
                // 모든 센서가 정상 상태인지 확인
                bool allNormal = true;
                lock (sensorDataLock)
                {
                    // 센서 알림 상태 확인
                    for (int i = 1; i <= SensorCount; i++)
                    {
                        if (sensorAlertStates[i] != 0)
                        {
                            allNormal = false;
                            break;
                        }
                    }
                }
                
                if (allNormal)
                {
                    // 모든 센서가 정상 상태이면 정기 로깅
                    LogSensorData("정상");
                }
            }));
        }

        private void PositionLogForm()
        {
            if (logForm == null) return;

            var owner = this;
            int x = owner.Left + (owner.Width - logForm.Width) / 2;
            int y = owner.Top + (owner.Height - logForm.Height) / 2;

            x = Math.Max(0, x);
            y = Math.Max(0, y);

            logForm.Location = new Point(x, y);
        }

        private string[] GetLogEntries()
        {
            return logService.GetLogEntries();
        }

        private void UpdateLogPreview()
        {
            lstLogPreview.BeginUpdate();
            lstLogPreview.Items.Clear();

            var logs = logService.LogHistory;
            int startIndex = Math.Max(0, logs.Count - 10);
            for (int i = logs.Count - 1; i >= startIndex; i--)
            {
                lstLogPreview.Items.Add(logs[i]);
            }

            lstLogPreview.EndUpdate();
        }

        private void SetOperationalControlsEnabled(bool enabled)
        {
            btnFarm1.Enabled = enabled;
            btnFarm2.Enabled = enabled;
            btnFarm3.Enabled = enabled;
            btnWebConnect.Enabled = enabled;
            btnEthercatPower.Enabled = enabled;
            btnManageFarm1.Enabled = enabled;
            btnManageFarm2.Enabled = enabled;
            btnManageFarm3.Enabled = enabled;

            for (int i = 1; i <= 4; i++)
            {
                var trackMin = FindSensorControl<TrackBar>($"trackSensor{i}Min");
                var trackMax = FindSensorControl<TrackBar>($"trackSensor{i}Max");
                var btnEdit = FindSensorControl<Button>($"btnEditSensorValue{i}");
                if (trackMin != null)
                    trackMin.Enabled = enabled;
                if (trackMax != null)
                    trackMax.Enabled = enabled;
                if (btnEdit != null)
                    btnEdit.Enabled = enabled;
            }
        }

        private void UpdateFarmButtonStyles()
        {
            Color activeColor = Color.LightGreen;
            Color inactiveColor = Color.WhiteSmoke;
            Color disabledColor = Color.Gainsboro;

            if (!powerOn)
            {
                btnFarm1.BackColor = disabledColor;
                btnFarm2.BackColor = disabledColor;
                btnFarm3.BackColor = disabledColor;
                return;
            }

            btnFarm1.BackColor = currentFarm == 1 ? activeColor : inactiveColor;
            btnFarm2.BackColor = currentFarm == 2 ? activeColor : inactiveColor;
            btnFarm3.BackColor = currentFarm == 3 ? activeColor : inactiveColor;
        }

        private T FindSensorControl<T>(string name) where T : Control
        {
            var controls = panelMain.Controls.Find(name, true);
            if (controls.Length > 0)
                return controls[0] as T;
            return null;
        }

        private void UpdateThresholdLabels(int index, int minValue, int maxValue)
        {
            var lblMin = FindSensorControl<Label>($"lblSensorMinValue{index}");
            var lblMax = FindSensorControl<Label>($"lblSensorMaxValue{index}");
            if (index == 2)
            {
                if (lblMin != null)
                    lblMin.Text = $"최소값: {minValue}도";
                if (lblMax != null)
                    lblMax.Text = $"최대값: {maxValue}도";
            }
            else
            {
                if (lblMin != null)
                    lblMin.Text = $"최소값: {minValue}";
                if (lblMax != null)
                    lblMax.Text = $"최대값: {maxValue}";
            }
        }

        private void ResetSensorAlerts()
        {
            for (int i = 1; i <= SensorCount; i++)
            {
                sensorAlertStates[i] = 0;
            }
        }

        private void ResetSensorVisuals()
        {
            for (int i = 1; i <= SensorCount; i++)
            {
                var bar = FindSensorControl<ProgressBar>($"barSensor{i}");
                if (bar != null) bar.Value = bar.Minimum;

                var lamp = FindSensorControl<Panel>($"lampSensor{i}");
                if (lamp != null) lamp.BackColor = Color.LightGray;

                var lbl = FindSensorControl<Label>($"lblSensorValue{i}");
                if (lbl != null) lbl.Text = "값: -";
                
                sensorOffsets[i] = 0.0; // 오프셋 초기화
                
                // 현재 농장의 오프셋도 초기화 (농장별 오프셋 독립 관리)
                if (!farmOffsets.ContainsKey(currentFarm))
                {
                    farmOffsets[currentFarm] = new double[SensorCount + 1];
                }
                farmOffsets[currentFarm][i] = 0.0;
            }
            ResetSensorAlerts();
        }

        private void HandleThresholdChanged(int index, bool isMinTrack)
        {
            var (minTrack, maxTrack, _, _) = GetThresholdControls(index);
            if (minTrack == null || maxTrack == null) return;

            if (isMinTrack && minTrack.Value > maxTrack.Value)
            {
                maxTrack.Value = minTrack.Value;
            }
            else if (!isMinTrack && maxTrack.Value < minTrack.Value)
            {
                minTrack.Value = maxTrack.Value;
            }

            // 온도 센서(index=2)의 경우 TrackBar 값을 실제 온도값으로 변환
            int actualMin = minTrack.Value;
            int actualMax = maxTrack.Value;
            
            if (index == 2) // 온도 센서
            {
                // TrackBar 값(0-100)을 실제 온도값(0-40℃)으로 변환
                // TrackBar 0 = 0℃, TrackBar 100 = 40℃
                actualMin = (int)Math.Round((minTrack.Value / 100.0) * 40);
                actualMax = (int)Math.Round((maxTrack.Value / 100.0) * 40);
            }

            UpdateThresholdLabels(index, actualMin, actualMax);

            if (farmSettings.TryGetValue(currentFarm, out var thresholds) && thresholds.Length >= index)
            {
                thresholds[index - 1].Min = actualMin;
                thresholds[index - 1].Max = actualMax;
            }

            // 임계값 변경 시 저장
            SaveSensorThresholds();

            // 임계값 변경 시 상태를 초기화하지 않음 (다음 센서값 업데이트에서 자동으로 재평가됨)
            // sensorAlertStates[index] = 0; // 제거: 이렇게 하면 정상 복귀 로그가 기록되지 않음
        }

        private (TrackBar minTrack, TrackBar maxTrack, Label lblMin, Label lblMax) GetThresholdControls(int index)
        {
            var minTrack = FindSensorControl<TrackBar>($"trackSensor{index}Min");
            var maxTrack = FindSensorControl<TrackBar>($"trackSensor{index}Max");
            var lblMin = FindSensorControl<Label>($"lblSensorMinValue{index}");
            var lblMax = FindSensorControl<Label>($"lblSensorMaxValue{index}");
            return (minTrack, maxTrack, lblMin, lblMax);
        }

        private void btnEthercatPower_Click(object sender, EventArgs e)
        {
            if (!powerOn)
            {
                Log("⚠️ 메인 전원이 OFF 상태에서는 EtherCAT 전원을 제어할 수 없음");
                return;
            }

            // 토글 형식: 현재 상태를 반전
            if (!ethercatPowerOn)
            {
                // 켜기 시도
                SetEthercatStatus(EthercatConnectionStatus.Connecting);
                if (ConnectToPlc())
                {
                    ethercatPowerOn = true;
                    SetEthercatStatus(EthercatConnectionStatus.Connected);
                    Log("EtherCAT 전원 켜짐 (UI 버튼)");
                    // WriteDigitalOutputToPlc(); // 램프 출력 기능 비활성화
                }
                else
                {
                    ethercatPowerOn = false;
                    SetEthercatStatus(EthercatConnectionStatus.Error);
                    Log("EtherCAT 전원 연결 실패");
                }
            }
            else
            {
                // 끄기
                ethercatPowerOn = false;
                DisconnectFromPlc();
                SetEthercatStatus(EthercatConnectionStatus.Off);
                Log("EtherCAT 전원 꺼짐 (UI 버튼)");
            }
        }

        private void SetEthercatStatus(EthercatConnectionStatus status)
        {
            ethercatStatus = status;

            if (lblEthercatStatus != null)
            {
                switch (status)
                {
                    case EthercatConnectionStatus.Off:
                        lblEthercatStatus.Text = "연결상태: OFF";
                        lblEthercatStatus.ForeColor = Color.Red;
                        break;
                    case EthercatConnectionStatus.Connecting:
                        lblEthercatStatus.Text = "연결상태: 연결 중...";
                        lblEthercatStatus.ForeColor = Color.DarkOrange;
                        break;
                    case EthercatConnectionStatus.Connected:
                        lblEthercatStatus.Text = "연결상태: 연결됨";
                        lblEthercatStatus.ForeColor = Color.Green;
                        break;
                    case EthercatConnectionStatus.Error:
                        lblEthercatStatus.Text = "연결상태: 오류";
                        lblEthercatStatus.ForeColor = Color.Red;
                        break;
                }
            }

            if (btnEthercatPower != null)
            {
                Color bg;
                switch (status)
                {
                    case EthercatConnectionStatus.Connected:
                        bg = Color.LightGreen;
                        break;
                    case EthercatConnectionStatus.Connecting:
                        bg = Color.Khaki;
                        break;
                    case EthercatConnectionStatus.Error:
                        bg = Color.MistyRose;
                        break;
                    default:
                        bg = Color.LightGray;
                        break;
                }

                btnEthercatPower.BackColor = bg;
            }
        }

        private void btnManageFarm_Click(object sender, EventArgs e)
        {
            if (!powerOn)
            {
                Log("⚠️ 전원 OFF 상태에서는 스마트팜 관리 불가");
                return;
            }

            Button btn = sender as Button;
            if (btn == null || btn.Tag == null)
                return;

            int farmId = (int)btn.Tag;
            string title = $"스마트팜 {farmId} 관리";
            string existingCrop = farmCropNames.ContainsKey(farmId) ? farmCropNames[farmId] : string.Empty;
            string existingNote = farmNotes.ContainsKey(farmId) ? farmNotes[farmId] : string.Empty;

            using (var dialog = new FarmManageForm(title, existingCrop, existingNote, FlaskServerUrl, GetCropListFromFlask))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string newCropName = dialog.CropName;
                    string newNote = dialog.AdditionalNote;
                    
                    // 작물 선택 여부 확인
                    bool cropChanged = !existingCrop.Equals(newCropName, StringComparison.OrdinalIgnoreCase);
                    
                    // Flask 서버 연결 상태 확인 (비동기)
                    if (!flaskServerRunning)
                    {
                        CheckFlaskServerConnection();
                    }
                    
                    // 작물이 선택되었고, Flask 서버에 연결되어 있으면 작물 정보 자동 설정
                    if (!string.IsNullOrEmpty(newCropName) && flaskServerRunning)
                    {
                        // Flask 서버에서 작물 정보 가져오기
                        Task.Run(async () =>
                        {
                            try
                            {
                                var cropInfo = await GetCropInfoFromFlask(newCropName);
                                if (cropInfo != null && cropInfo.ContainsKey("success") && 
                                    (bool)cropInfo["success"])
                                {
                                    BeginInvoke(new Action(() =>
                                    {
                                        try
                                        {
                                            // 작물 정보로 센서 임계값 자동 설정 (먼저 실행)
                                            ApplyCropConditions(farmId, cropInfo);
                                            
                                            // 추가 정보 생성 (항상 새로 생성하여 최신 정보 반영)
                                            string generatedNote = GenerateCropDetailedInfo(newCropName, cropInfo);
                                            
                                            // 사용자가 수동으로 입력한 정보가 있으면 그것을 사용, 없으면 생성된 정보 사용
                                            if (string.IsNullOrWhiteSpace(newNote) || cropChanged)
                                            {
                                                newNote = generatedNote;
                                            }
                                            
                                            // 정보 저장
                                            farmCropNames[farmId] = newCropName;
                                            farmNotes[farmId] = newNote;
                                            SaveFarmInfo();
                                            
                                            Log($"✅ 스마트팜 {farmId} 작물 정보 자동 설정 완료: {newCropName}");
                                            Log($"   → 센서 임계값이 {newCropName} 재배에 최적화되었습니다.");
                                            Log($"   → 작물 상세 정보가 생성되었습니다.");
                                            
                                            // Flask 서버에 작물 정보 업데이트 알림
                                            SendFarmChangeToFlask();
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"⚠️ 작물 정보 자동 설정 오류: {ex.Message}");
                                            // 기본 저장은 수행
                                            farmCropNames[farmId] = newCropName;
                                            farmNotes[farmId] = newNote;
                                            SaveFarmInfo();
                                            SendFarmChangeToFlask();
                                        }
                                    }));
                                }
                                else
                                {
                                    // 작물 정보를 찾을 수 없으면 기본 설정으로 저장
                                    BeginInvoke(new Action(() =>
                                    {
                                        farmCropNames[farmId] = newCropName;
                                        farmNotes[farmId] = newNote;
                                        SaveFarmInfo();
                                        Log($"⚠️ 스마트팜 {farmId} 작물 정보 저장: {newCropName} (최적 조건 정보 없음)");
                                        SendFarmChangeToFlask();
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                                // 오류 발생 시 기본 저장만 수행
                                BeginInvoke(new Action(() =>
                                {
                                    farmCropNames[farmId] = newCropName;
                                    farmNotes[farmId] = newNote;
                                    SaveFarmInfo();
                                    Log($"⚠️ 작물 정보 자동 설정 실패: {ex.Message}");
                                    Log($"   스마트팜 {farmId} 작물 정보는 저장되었지만 최적 조건은 수동으로 설정해주세요.");
                                    SendFarmChangeToFlask();
                                }));
                            }
                        });
                    }
                    else
                    {
                        // Flask 서버 연결 안 됨 또는 작물이 없음
                        farmCropNames[farmId] = newCropName;
                        farmNotes[farmId] = newNote;
                        SaveFarmInfo();
                        
                        Log($"🛠️ 스마트팜 {farmId} 정보 업데이트: {newCropName ?? "없음"}");
                        if (!flaskServerRunning)
                        {
                            Log($"⚠️ Flask 서버에 연결되지 않았습니다. 작물 최적 조건을 가져올 수 없습니다.");
                        }
                        
                        // Flask 서버에 작물 정보 업데이트 알림
                        if (flaskServerRunning)
                        {
                            SendFarmChangeToFlask();
                        }
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 프로그램 종료 시 센서 임계값 및 스마트팜 정보 저장
            SaveSensorThresholds();
            SaveFarmInfo();
            
            DisconnectFromPlc();
            base.OnFormClosing(e);
        }

        private bool ConnectToPlc()
        {
            DisconnectFromPlc(false);

            try
            {
                bool result = plcService.Connect(msg => Log(msg));
                adsConnected = true;

                sensorInitializationTime = DateTime.Now;
                ResetSensorAlerts();

                sensorLogTimer?.Dispose();
                sensorLogTimer = new System.Threading.Timer(SensorLogTimerCallback, null, sensorLogInterval, sensorLogInterval);
                lastSensorLogTime = DateTime.Now;

                return result;
            }
            catch (Exception ex)
            {
                DisconnectFromPlc(false);
                MessageBox.Show(ex.Message, "PLC 연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log(ex.Message);
                return false;
            }
        }

        private void DisconnectFromPlc(bool log = true)
        {
            sensorLogTimer?.Dispose();
            sensorLogTimer = null;
            sensorInitializationTime = DateTime.MinValue;
            ResetSensorAlerts();

            plcService.Disconnect(log, msg => Log(msg));
            adsConnected = false;
            buttonStatesInitialized = false;
        }

        // PollPlcValues moved to PlcService - events handled via plcService.AnalogDataReceived/DigitalInputReceived/ConnectionError

        private void UpdateSensorsFromPlc(AnalogInputData data)
        {
            double humidityPercent = Clamp(ScaleLinear(data.Humidity_Sensor, 5500, 7550, 68, 94), 0, 100);
            double temperatureCelsius = Clamp(ConvertTemperatureCelsius(data.Temperature_Sensor), -10, 60);
            
            // 압력 센서 변환 개선: 원시 값 범위 체크 및 안전한 변환
            double lightPercent = 0.0;
            if (data.Pressure_Sensor < 5)
            {
                // 최소값보다 작으면 0%로 설정
                lightPercent = 0.0;
            }
            else if (data.Pressure_Sensor > 3575)
            {
                // 최대값보다 크면 100%로 설정
                lightPercent = 100.0;
            }
            else
            {
                // 정상 범위 내에서는 선형 변환
                lightPercent = ScaleLinear(data.Pressure_Sensor, 5, 3575, 0, 100);
            }
            lightPercent = Clamp(lightPercent, 0, 100);
            
            double soilMoisturePercent = Clamp(ScaleLinear(data.Vibration_Sensor, 450, 6500, 0, 100), 0, 100);

            BeginInvoke(new Action(() =>
            {
                if (ethercatPowerOn && ethercatStatus != EthercatConnectionStatus.Connected)
                    SetEthercatStatus(EthercatConnectionStatus.Connected);

                // 장비 값에 오프셋을 더해서 표시
                double humidityWithOffset = humidityPercent + sensorOffsets[1];
                ApplySensorValue(1,
                    ClampToProgress(humidityWithOffset),
                    $"{humidityWithOffset:F1}%",
                    data.Humidity_Sensor,
                    humidityWithOffset);

                double temperatureWithOffset = temperatureCelsius + sensorOffsets[2];
                int temperatureBarValue = (int)Math.Round(Clamp(temperatureWithOffset, 10, 40));
                ApplySensorValue(2,
                    temperatureBarValue,
                    $"{temperatureWithOffset:F1}℃",
                    data.Temperature_Sensor,
                    temperatureWithOffset);

                double lightWithOffset = lightPercent + sensorOffsets[3];
                // 압력 센서 디버깅: 원시 값이 비정상적이면 로그 출력 (주기적으로만)
                if (data.Pressure_Sensor < 0 || data.Pressure_Sensor > 4000)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 압력 센서 원시 값 이상: {data.Pressure_Sensor} (변환된 값: {lightPercent:F1}%)");
                }
                // 압력 센서 오프셋 적용 확인 (주기적으로만 디버그 로그)
                if (DateTime.Now - lastPressureLogTime > TimeSpan.FromSeconds(5))
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 압력 센서 오프셋 적용: 원본={lightPercent:F1}%, 오프셋={sensorOffsets[3]:F1}, 최종={lightWithOffset:F1}%");
                    lastPressureLogTime = DateTime.Now;
                }
                ApplySensorValue(3,
                    ClampToProgress(lightWithOffset),
                    $"{lightWithOffset:F1}%",
                    data.Pressure_Sensor,
                    lightWithOffset);

                double soilMoistureWithOffset = soilMoisturePercent + sensorOffsets[4];
                ApplySensorValue(4,
                    ClampToProgress(soilMoistureWithOffset),
                    $"{soilMoistureWithOffset:F1}%",
                    data.Vibration_Sensor,
                    soilMoistureWithOffset);

                // 센서 데이터 저장 (Flask 서버용)
                // 오프셋이 적용된 값과 원본 값을 모두 저장
                lock (sensorDataLock)
                {
                    // 오프셋이 적용된 값 (AI 제어 시 이 값을 기준으로 계산)
                    currentSensorData.Humidity = humidityWithOffset;
                    currentSensorData.Temperature = temperatureWithOffset;
                    currentSensorData.Light = lightWithOffset;
                    currentSensorData.SoilMoisture = soilMoistureWithOffset;
                    // 원본 센서 값 (오프셋 계산 시 사용)
                    currentSensorData.RawHumidity = humidityPercent;
                    currentSensorData.RawTemperature = temperatureCelsius;
                    currentSensorData.RawLight = lightPercent;
                    currentSensorData.RawSoilMoisture = soilMoisturePercent;
                    currentSensorData.LastUpdate = DateTime.Now;
                }

                // Flask 서버로 센서 데이터 전송 (주기적으로)
                if (flaskServerRunning)
                {
                    SendSensorDataToFlask();
                    
                    // AI 자동 제어 활성화 시 지속적으로 모니터링
                    // 최적 범위 내에서는 조정하지 않고, 외부 요인으로 값이 벗어나면 실시간으로 자동 조정
                    if (aiAutoControlEnabled && (DateTime.Now - lastAIAutoControlCheck) >= aiAutoControlCheckInterval)
                    {
                        lastAIAutoControlCheck = DateTime.Now;
                        // 비동기 메서드를 fire-and-forget 방식으로 호출 (최적 범위 체크 및 필요시 조정)
                        _ = CheckAndAutoControlIfNeeded(humidityWithOffset, temperatureWithOffset, lightWithOffset, soilMoistureWithOffset);
                    }
                }
                else
                {
                    // Flask 서버 연결 상태 재확인 (연결 끊김 복구)
                    Task.Run(() =>
                    {
                        bool reconnected = CheckFlaskServerConnection();
                        if (reconnected)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                Log("✅ Flask 서버 연결 복구됨");
                            }));
                        }
                    });
                }

                // AI 모델에 센서 데이터 전달은 ApplySensorValue에서 처리
                // (정상 복귀 감지 시 복귀 정보도 함께 전달하기 위해)

                UpdateDigitalOutputs();
            }));
        }
        
        private async Task CheckAndAutoControlIfNeeded(double humidity, double temperature, double light, double soilMoisture)
        {
            try
            {
                // 현재 작물의 최적 조건 가져오기
                string cropName = farmCropNames.ContainsKey(currentFarm) ? farmCropNames[currentFarm] : "";
                if (string.IsNullOrEmpty(cropName))
                {
                    if (currentFarm == 1)
                        cropName = "사과";
                    else
                        cropName = "기본";
                }
                
                // Flask 서버에서 작물 정보 가져오기
                Dictionary<string, object> cropInfo = await GetCropInfoFromFlask(cropName);
                if (cropInfo == null || !cropInfo.ContainsKey("conditions"))
                {
                    return; // 작물 정보를 가져올 수 없으면 스킵
                }
                
                var conditions = Newtonsoft.Json.Linq.JObject.FromObject(cropInfo["conditions"]);
                
                // 최적 범위 확인 (optimal_min ~ optimal_max)
                // 허용 오차 추가: 최적 범위 경계 근처에서 오실레이션 방지
                bool needsControl = false;
                
                // 습도 확인
                var humidityCond = conditions["humidity"];
                if (humidityCond != null)
                {
                    int optimalMin = humidityCond["optimal_min"]?.Value<int>() ?? 50;
                    int optimalMax = humidityCond["optimal_max"]?.Value<int>() ?? 80;
                    double tolerance = (optimalMax - optimalMin) * 0.05; // 최적 범위의 5% 오차 허용
                    if (humidity < (optimalMin - tolerance) || humidity > (optimalMax + tolerance))
                    {
                        needsControl = true;
                    }
                }
                
                // 온도 확인
                var temperatureCond = conditions["temperature"];
                if (temperatureCond != null)
                {
                    int optimalMin = temperatureCond["optimal_min"]?.Value<int>() ?? 15;
                    int optimalMax = temperatureCond["optimal_max"]?.Value<int>() ?? 25;
                    double tolerance = (optimalMax - optimalMin) * 0.05; // 최적 범위의 5% 오차 허용
                    if (temperature < (optimalMin - tolerance) || temperature > (optimalMax + tolerance))
                    {
                        needsControl = true;
                    }
                }
                
                // 채광 확인
                var lightCond = conditions["light"];
                if (lightCond != null)
                {
                    int optimalMin = lightCond["optimal_min"]?.Value<int>() ?? 50;
                    int optimalMax = lightCond["optimal_max"]?.Value<int>() ?? 80;
                    double tolerance = (optimalMax - optimalMin) * 0.05; // 최적 범위의 5% 오차 허용
                    if (light < (optimalMin - tolerance) || light > (optimalMax + tolerance))
                    {
                        needsControl = true;
                    }
                }
                
                // 토양습도 확인
                var soilMoistureCond = conditions["soil_moisture"];
                if (soilMoistureCond != null)
                {
                    int optimalMin = soilMoistureCond["optimal_min"]?.Value<int>() ?? 40;
                    int optimalMax = soilMoistureCond["optimal_max"]?.Value<int>() ?? 60;
                    double tolerance = (optimalMax - optimalMin) * 0.05; // 최적 범위의 5% 오차 허용
                    if (soilMoisture < (optimalMin - tolerance) || soilMoisture > (optimalMax + tolerance))
                    {
                        needsControl = true;
                    }
                }
                
                // 최적 범위를 벗어났을 때만 자동 제어 실행
                // 최적 범위 내에 있으면 모니터링만 계속 (AI는 활성화 상태 유지, 조정은 하지 않음)
                if (needsControl)
                {
                    // 외부 요인으로 값이 최적 범위를 벗어났으므로 실시간으로 조정
                    // 마지막 실행 이후 충분한 시간이 지났는지 확인 (오버슈팅 방지)
                    if ((DateTime.Now - lastAIAutoControlExecution) >= aiAutoControlExecutionCooldown)
                    {
                        lastAIAutoControlExecution = DateTime.Now;
                        BeginInvoke(new Action(() =>
                        {
                            Log($"⚠️ 외부 요인으로 센서 값이 최적 범위를 벗어났습니다. AI 자동 제어로 실시간 조정 실행...");
                        }));
                        await Task.Run(() => ExecuteAIAutoControl());
                    }
                    else
                    {
                        // 쿨다운 중이면 스킵 (너무 자주 실행하지 않음)
                        System.Diagnostics.Debug.WriteLine($"AI 자동 제어 쿨다운 중... (남은 시간: {(aiAutoControlExecutionCooldown - (DateTime.Now - lastAIAutoControlExecution)).TotalSeconds:F1}초)");
                    }
                }
                else
                {
                    // 최적 범위 내에 있음 - AI는 활성화 상태로 계속 모니터링 (조정은 하지 않음)
                    // 외부 요인으로 값이 변하면 자동으로 감지하여 조정됨
                    System.Diagnostics.Debug.WriteLine($"✅ 모든 센서 값이 최적 범위 내에 있습니다. AI 모니터링 계속 중 (조정 불필요).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"자동 제어 체크 오류: {ex.Message}");
            }
        }

        private void ApplySensorValue(int index, int barValue, string formattedValue, int rawValue, double comparisonValue)
        {
            var bar = FindSensorControl<ProgressBar>($"barSensor{index}");
            var lamp = FindSensorControl<Panel>($"lampSensor{index}");
            var lbl = FindSensorControl<Label>($"lblSensorValue{index}");

            if (bar != null)
            {
                int clamped = Math.Min(bar.Maximum, Math.Max(bar.Minimum, barValue));
                bar.Value = clamped;
            }

            if (lbl != null)
            {
                lbl.Text = $"값: {formattedValue} (원시: {rawValue})";
            }

            var (minTrack, maxTrack, _, _) = GetThresholdControls(index);
            double lower = minTrack?.Value ?? double.MinValue;
            double upper = maxTrack?.Value ?? double.MaxValue;
            int alertState = 0;

            if (comparisonValue < lower)
            {
                alertState = -1;
            }
            else if (comparisonValue > upper)
            {
                alertState = 1;
            }

            if (lamp != null)
            {
                if (alertState == -1)
                {
                    lamp.BackColor = Color.DeepSkyBlue;
                }
                else if (alertState == 1)
                {
                    lamp.BackColor = Color.Red;
                }
                else
                {
                    lamp.BackColor = Color.LightGreen;
                }
            }

            // 상태 변화 감지 및 로그 기록
            int previousState = sensorAlertStates[index];
            
            // 초기 연결 후 일정 시간 동안은 경고 표시 안 함 (센서 안정화 시간)
            bool isInitializationPeriod = (DateTime.Now - sensorInitializationTime) < sensorStabilizationTime;
            
            if (previousState != alertState)
            {
                string sensorName = sensorDisplayNames[index];

                if (alertState == -1)
                {
                    sensorAlertStates[index] = alertState;
                    
                    // 초기화 기간이 아니고, 센서 값이 유효한 경우에만 경고 표시
                    if (!isInitializationPeriod && rawValue != 0)
                    {
                        // 웹 표준 형식: 4개 센서 데이터 모두 포함
                        LogSensorData("이상");
                        
                        string warningMsg = $"스마트팜 {currentFarm} {sensorName} 값 낮음 ({formattedValue})";
                        
                        // 자동 제어 시도
                        if (autoControlEnabled && powerOn && ethercatPowerOn)
                        {
                            TryAutoControl(index, -1, sensorName, comparisonValue, lower, upper);
                        }
                    }
                }
                else if (alertState == 1)
                {
                    sensorAlertStates[index] = alertState;
                    
                    // 초기화 기간이 아니고, 센서 값이 유효한 경우에만 경고 표시
                    if (!isInitializationPeriod && rawValue != 0)
                    {
                        // 웹 표준 형식: 4개 센서 데이터 모두 포함
                        LogSensorData("이상");
                        
                        string warningMsg = $"스마트팜 {currentFarm} {sensorName} 값 높음 ({formattedValue})";
                        
                        // 자동 제어 시도
                        if (autoControlEnabled && powerOn && ethercatPowerOn)
                        {
                            TryAutoControl(index, 1, sensorName, comparisonValue, lower, upper);
                        }
                    }
                }
                else if (alertState == 0 && previousState != 0)
                {
                    // 주의값에서 정상값으로 복귀
                    sensorAlertStates[index] = alertState;
                    
                    // 웹 표준 형식: 4개 센서 데이터 모두 포함
                    LogSensorData("정상");
                    
                    string previousStatus = previousState == -1 ? "낮음" : "높음";
                }
                else
                {
                    // 다른 상태 변화 (예: 정상 -> 정상, 또는 예상치 못한 변화)
                    sensorAlertStates[index] = alertState;
                }
                
            }
            else
            {
                // 상태 변화 없음
                sensorAlertStates[index] = alertState;
            }
        }
        
        // 자동 제어 시도
        private void TryAutoControl(int sensorIndex, int alertDirection, string sensorName, 
            double currentValue, double lowerThreshold, double upperThreshold)
        {
            // 쿨다운 체크
            if (lastAutoControlTime.ContainsKey(sensorIndex))
            {
                if (DateTime.Now - lastAutoControlTime[sensorIndex] < autoControlCooldown)
                    return;
            }
            
            lastAutoControlTime[sensorIndex] = DateTime.Now;
            
            try
            {
                // PLC 출력 핀을 통한 자동 제어
                // 실제 하드웨어에 맞게 수정 필요
                string controlAction = "";
                
                if (sensorIndex == 1) // 습도
                {
                    if (alertDirection == -1) // 낮음
                    {
                        controlAction = "가습기 켜기";
                        // PLC 출력 핀 제어 코드 추가 필요
                        // 예: WriteToPlcOutput("Humidifier", true);
                    }
                    else if (alertDirection == 1) // 높음
                    {
                        controlAction = "환기 시스템 켜기";
                        // PLC 출력 핀 제어 코드 추가 필요
                    }
                }
                else if (sensorIndex == 2) // 온도
                {
                    if (alertDirection == -1) // 낮음
                    {
                        controlAction = "히터 켜기";
                        // PLC 출력 핀 제어 코드 추가 필요
                    }
                    else if (alertDirection == 1) // 높음
                    {
                        controlAction = "냉방 시스템 켜기";
                        // PLC 출력 핀 제어 코드 추가 필요
                    }
                }
                else if (sensorIndex == 3) // 채광
                {
                    if (alertDirection == -1) // 낮음
                    {
                        controlAction = "LED 조명 켜기";
                        // PLC 출력 핀 제어 코드 추가 필요
                    }
                }
                else if (sensorIndex == 4) // 토양습도
                {
                    if (alertDirection == -1) // 낮음
                    {
                        controlAction = "급수 시스템 작동";
                        // PLC 출력 핀 제어 코드 추가 필요
                    }
                    else if (alertDirection == 1) // 높음
                    {
                        controlAction = "배수 시스템 작동";
                        // PLC 출력 핀 제어 코드 추가 필요
                    }
                }
                
                if (!string.IsNullOrEmpty(controlAction))
                {
                    Log($"🤖 자동 제어: {controlAction} (센서: {sensorName}, 현재값: {currentValue:F1})");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ 자동 제어 실패: {ex.Message}");
            }
        }

        // ClampToProgress, Clamp, ScaleLinear, ConvertTemperatureCelsius moved to Services/SensorCalibration.cs
        private static int ClampToProgress(double value) => SensorCalibration.ClampToProgress(value);
        private static double Clamp(double value, double min, double max) => SensorCalibration.Clamp(value, min, max);
        private static double ScaleLinear(double raw, double rawMin, double rawMax, double valueMin, double valueMax) => SensorCalibration.ScaleLinear(raw, rawMin, rawMax, valueMin, valueMax);
        private static double ConvertTemperatureCelsius(int raw) => SensorCalibration.ConvertTemperatureCelsius(raw);

        private void HandleEditSensorValue(int index)
        {
            if (!powerOn)
            {
                Log("⚠️ 전원 OFF 상태에서는 센서 값을 수정할 수 없습니다");
                return;
            }

            string sensorName = sensorDisplayNames[index];
            double currentOffset = sensorOffsets[index];

            // 입력 다이얼로그 표시
            string prompt = $"{sensorName} 오프셋 값을 입력하세요:";
            string title = $"{sensorName} 오프셋 수정";
            string defaultValue = currentOffset != 0.0 ? currentOffset.ToString("F1") : "0.0";
            
            prompt += "\n(예: +10, -5, 0)";
            prompt += "\n장비 값에 이 값을 더해서 표시합니다.";

            using (var inputForm = new Form())
            {
                inputForm.Text = title;
                inputForm.Size = new Size(400, 180);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;

                Label lblPrompt = new Label
                {
                    Text = prompt,
                    AutoSize = true,
                    Location = new Point(15, 15)
                };

                TextBox txtValue = new TextBox
                {
                    Text = defaultValue,
                    Size = new Size(350, 25),
                    Location = new Point(15, 80)
                };

                Button btnOk = new Button
                {
                    Text = "확인",
                    DialogResult = DialogResult.OK,
                    Size = new Size(80, 30),
                    Location = new Point(155, 115)
                };

                Button btnCancel = new Button
                {
                    Text = "취소",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(80, 30),
                    Location = new Point(245, 115)
                };

                Button btnReset = new Button
                {
                    Text = "초기화",
                    Size = new Size(80, 30),
                    Location = new Point(65, 115)
                };
                btnReset.Click += (s, e) =>
                {
                    sensorOffsets[index] = 0.0;
                    // 현재 농장의 오프셋도 초기화 (농장별 오프셋 독립 관리)
                    if (!farmOffsets.ContainsKey(currentFarm))
                    {
                        farmOffsets[currentFarm] = new double[SensorCount + 1];
                    }
                    farmOffsets[currentFarm][index] = 0.0;
                    Log($"{sensorName} 오프셋 초기화 (0.0)");
                    inputForm.DialogResult = DialogResult.OK;
                    inputForm.Close();
                };

                inputForm.AcceptButton = btnOk;
                inputForm.CancelButton = btnCancel;
                inputForm.Controls.Add(lblPrompt);
                inputForm.Controls.Add(txtValue);
                inputForm.Controls.Add(btnOk);
                inputForm.Controls.Add(btnCancel);
                inputForm.Controls.Add(btnReset);

                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(txtValue.Text))
                    {
                        // 빈 값이면 초기화
                        sensorOffsets[index] = 0.0;
                        // 현재 농장의 오프셋도 초기화 (농장별 오프셋 독립 관리)
                        if (!farmOffsets.ContainsKey(currentFarm))
                        {
                            farmOffsets[currentFarm] = new double[SensorCount + 1];
                        }
                        farmOffsets[currentFarm][index] = 0.0;
                        Log($"{sensorName} 오프셋 초기화 (0.0)");
                    }
                    else if (double.TryParse(txtValue.Text, out double newOffset))
                    {
                        sensorOffsets[index] = newOffset;
                        // 현재 농장의 오프셋도 저장 (농장별 오프셋 독립 관리)
                        if (!farmOffsets.ContainsKey(currentFarm))
                        {
                            farmOffsets[currentFarm] = new double[SensorCount + 1];
                        }
                        farmOffsets[currentFarm][index] = newOffset;
                        string offsetText = newOffset >= 0 ? $"+{newOffset:F1}" : $"{newOffset:F1}";
                        Log($"{sensorName} 오프셋을 {offsetText}로 설정");
                        
                        // 즉시 UI 업데이트 (장비 값이 없어도 오프셋만 표시)
                        // 실제로는 다음 PLC 업데이트 시 적용됨
                    }
                    else
                    {
                        MessageBox.Show("올바른 숫자를 입력하세요. (예: +10, -5, 0)", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void HandleDigitalInput(DigitalInputData data)
        {
            BeginInvoke(new Action(() =>
            {
                // 비트 연산으로 버튼 상태 읽기
                // 비트 0: Button1, 비트 1: Button2, 비트 2: Button3, 비트 3: Button4
                bool btn1 = (data.Bits & (1 << 0)) != 0;
                bool btn2 = (data.Bits & (1 << 1)) != 0;
                bool btn3 = (data.Bits & (1 << 2)) != 0;
                bool btn4 = (data.Bits & (1 << 3)) != 0;

                // 첫 번째 읽기 시 현재 상태를 이전 상태로 설정 (엣지 감지 방지)
                if (!buttonStatesInitialized)
                {
                    previousButtonStates[1] = btn1;
                    previousButtonStates[2] = btn2;
                    previousButtonStates[3] = btn3;
                    previousButtonStates[4] = btn4;
                    buttonStatesInitialized = true;
                    Log($"버튼 초기 상태 - B1:{btn1} B2:{btn2} B3:{btn3} B4:{btn4} (Bits: 0x{data.Bits:X4})");
                    return; // 첫 번째는 엣지 감지하지 않음
                }

                // 버튼 1: 스마트팜 1 전환 (엣지 감지: false -> true)
                if (btn1 && !previousButtonStates[1] && powerOn)
                {
                    currentFarm = 1;
                    ApplyFarmSettingsToUI(currentFarm);
                    UpdateFarmButtonStyles();
                    ResetSensorAlerts();
                    Log("스마트팜 1번 선택 (장비 버튼 1)");
                }
                previousButtonStates[1] = btn1;

                // 버튼 2: 스마트팜 2 전환
                if (btn2 && !previousButtonStates[2] && powerOn)
                {
                    currentFarm = 2;
                    ApplyFarmSettingsToUI(currentFarm);
                    UpdateFarmButtonStyles();
                    ResetSensorAlerts();
                    Log("스마트팜 2번 선택 (장비 버튼 2)");
                }
                previousButtonStates[2] = btn2;

                // 버튼 3: 스마트팜 3 전환
                if (btn3 && !previousButtonStates[3] && powerOn)
                {
                    currentFarm = 3;
                    ApplyFarmSettingsToUI(currentFarm);
                    UpdateFarmButtonStyles();
                    ResetSensorAlerts();
                    Log("스마트팜 3번 선택 (장비 버튼 3)");
                }
                previousButtonStates[3] = btn3;

                // 버튼 4: AI 자동제어 ON/OFF 토글
                if (btn4 && !previousButtonStates[4] && powerOn)
                {
                    // UI 스레드에서 AI 자동제어 토글 실행
                    BeginInvoke(new Action(() =>
                    {
                        // 전원과 연결 상태 확인
                        if (!powerOn || !adsConnected)
                        {
                            Log("⚠️ 전원을 켜고 장비에 연결한 후 사용할 수 있습니다. (장비 버튼 4)");
                            return;
                        }
                        
                        // Flask 서버 연결 확인
                        if (!flaskServerRunning)
                        {
                            Log("⚠️ Flask 서버에 연결할 수 없습니다. 웹 연결 버튼을 먼저 클릭하여 서버를 확인하세요. (장비 버튼 4)");
                            return;
                        }
                        
                        // AI 자동 제어 활성화/비활성화 토글
                        aiAutoControlEnabled = !aiAutoControlEnabled;
                        
                        if (aiAutoControlEnabled)
                        {
                            btnAutoControl.BackColor = Color.LightGreen;
                            btnAutoControl.Text = "AI 자동제어 ON";
                            Log("🤖 AI 자동 제어 활성화 (장비 버튼 4): 최적값 내에서는 모니터링만 계속하며, 외부 요인으로 값이 벗어나면 실시간으로 자동 조정합니다.");
                            // 즉시 한 번 실행 (현재 상태 확인 및 필요시 조정)
                            Log("🤖 AI 자동 제어 시작...");
                            Task.Run(() => ExecuteAIAutoControl());
                        }
                        else
                        {
                            btnAutoControl.BackColor = Color.LightBlue;
                            btnAutoControl.Text = "AI 자동제어";
                            Log("⏸️ AI 자동 제어 비활성화 (장비 버튼 4): 수동 제어만 가능합니다.");
                        }
                    }));
                }
                previousButtonStates[4] = btn4;
            }));
        }

        private void UpdateDigitalOutputs()
        {
            if (!plcService.IsConnected)
                return;

            try
            {
                DigitalOutputData output = new DigitalOutputData();
                output.Bits = 0;

                if (powerOn)
                    output.Bits |= (ushort)(1 << 0);

                bool anyLow = false;
                for (int i = 1; i <= SensorCount; i++)
                {
                    if (sensorAlertStates[i] == -1) { anyLow = true; break; }
                }
                if (anyLow)
                    output.Bits |= (ushort)(1 << 1);

                bool anyHigh = false;
                for (int i = 1; i <= SensorCount; i++)
                {
                    if (sensorAlertStates[i] == 1) { anyHigh = true; break; }
                }
                if (anyHigh)
                    output.Bits |= (ushort)(1 << 2);

                bool allNormal = true;
                for (int i = 1; i <= SensorCount; i++)
                {
                    if (sensorAlertStates[i] != 0) { allNormal = false; break; }
                }
                if (allNormal && powerOn)
                    output.Bits |= (ushort)(1 << 3);

                plcService.WriteDigitalOutput(output);
            }
            catch (Exception ex)
            {
                Log($"램프 출력 실패: {ex.Message}");
            }
        }

        // AnalogInputData, DigitalInputData, DigitalOutputData moved to Models/PlcDataStructs.cs

        // 웹 서버용 JSON 데이터 생성 메서드
        private string GetLogsJson()
        {
            return logService.GetLogsJson();
        }

        private string GetSensorDataJson()
        {
            lock (sensorDataLock)
            {
                // UI 컨트롤에 직접 접근하지 않고 저장된 임계값 사용 (크로스 스레드 오류 방지)
                int min1 = 30, max1 = 70;
                int min2 = 10, max2 = 40;
                int min3 = 30, max3 = 70;
                int min4 = 30, max4 = 70;
                
                if (farmSettings.ContainsKey(currentFarm) && farmSettings[currentFarm] != null)
                {
                    var thresholds = farmSettings[currentFarm];
                    if (thresholds.Length >= 4)
                    {
                        min1 = thresholds[0].Min;
                        max1 = thresholds[0].Max;
                        min2 = thresholds[1].Min;
                        max2 = thresholds[1].Max;
                        min3 = thresholds[2].Min;
                        max3 = thresholds[2].Max;
                        min4 = thresholds[3].Min;
                        max4 = thresholds[3].Max;
                    }
                }

                // 농장 정보 배열 생성
                var farms = new List<object>();
                for (int farm = 1; farm <= 3; farm++)
                {
                    farms.Add(new
                    {
                        id = farm,
                        cropName = farmCropNames.ContainsKey(farm) ? farmCropNames[farm] : "",
                        note = farmNotes.ContainsKey(farm) ? farmNotes[farm] : ""
                    });
                }

                var data = new
                {
                    currentFarm = currentFarm,
                    powerOn = powerOn,
                    connected = adsConnected,
                    lastUpdate = currentSensorData.LastUpdate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    farms = farms.ToArray(),
                    sensors = new[]
                    {
                        new
                        {
                            id = 1,
                            name = "습도",
                            value = $"{currentSensorData.Humidity:F1}%",
                            rawValue = currentSensorData.Humidity,
                            min = min1,
                            max = max1,
                            percentage = Math.Min(100, Math.Max(0, (int)currentSensorData.Humidity)),
                            status = sensorAlertStates[1] == 0 ? "정상" : (sensorAlertStates[1] < 0 ? "낮음" : "높음"),
                            offset = sensorOffsets[1]
                        },
                        new
                        {
                            id = 2,
                            name = "온도",
                            value = $"{currentSensorData.Temperature:F1}℃",
                            rawValue = currentSensorData.Temperature,
                            min = min2,
                            max = max2,
                            percentage = Math.Min(100, Math.Max(0, (int)((currentSensorData.Temperature - 10) / 30 * 100))),
                            status = sensorAlertStates[2] == 0 ? "정상" : (sensorAlertStates[2] < 0 ? "낮음" : "높음"),
                            offset = sensorOffsets[2]
                        },
                        new
                        {
                            id = 3,
                            name = "채광",
                            value = $"{currentSensorData.Light:F1}%",
                            rawValue = currentSensorData.Light,
                            min = min3,
                            max = max3,
                            percentage = Math.Min(100, Math.Max(0, (int)currentSensorData.Light)),
                            status = sensorAlertStates[3] == 0 ? "정상" : (sensorAlertStates[3] < 0 ? "낮음" : "높음"),
                            offset = sensorOffsets[3]
                        },
                        new
                        {
                            id = 4,
                            name = "토양습도",
                            value = $"{currentSensorData.SoilMoisture:F1}%",
                            rawValue = currentSensorData.SoilMoisture,
                            min = min4,
                            max = max4,
                            percentage = Math.Min(100, Math.Max(0, (int)currentSensorData.SoilMoisture)),
                            status = sensorAlertStates[4] == 0 ? "정상" : (sensorAlertStates[4] < 0 ? "낮음" : "높음"),
                            offset = sensorOffsets[4]
                        }
                    }
                };

                return ToJson(data);
            }
        }

        private string GetFarmDataJson()
        {
            var data = new
            {
                currentFarm = currentFarm,
                powerOn = powerOn,
                connected = adsConnected,
                farms = new[]
                {
                    new
                    {
                        id = 1,
                        cropName = farmCropNames.ContainsKey(1) ? farmCropNames[1] : "",
                        note = farmNotes.ContainsKey(1) ? farmNotes[1] : ""
                    },
                    new
                    {
                        id = 2,
                        cropName = farmCropNames.ContainsKey(2) ? farmCropNames[2] : "",
                        note = farmNotes.ContainsKey(2) ? farmNotes[2] : ""
                    },
                    new
                    {
                        id = 3,
                        cropName = farmCropNames.ContainsKey(3) ? farmCropNames[3] : "",
                        note = farmNotes.ContainsKey(3) ? farmNotes[3] : ""
                    }
                }
            };

            return ToJson(data);
        }

        // 간단한 JSON 변환 메서드
        private string ToJson(object obj)
        {
            return JsonHelper.ToJson(obj);
        }

        private string EscapeJson(string str)
        {
            return JsonHelper.EscapeJson(str);
        }

        private bool SaveLogsToFile(string requestBody)
        {
            return logService.SaveLogsToFile(requestBody);
        }

        private string LoadLogsFromFile(string filePath)
        {
            return logService.LoadLogsFromFile(filePath);
        }

        private string ParseLogsFromContent(string fileContent)
        {
            return logService.ParseLogsFromContent(fileContent);
        }

        private bool AddLogsToHistory(string logsJson)
        {
            return logService.AddLogsToHistory(logsJson);
        }

    }
}
