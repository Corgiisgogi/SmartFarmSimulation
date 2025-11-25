# AI 농장 시뮬레이터 코드 개선 사항

## 🔴 긴급 수정 필요 (Critical Issues)

### 1. **메모리 누수: logHistory 무한 증가**
**문제점:**
- `logHistory` 리스트가 계속 증가하여 장기 실행 시 메모리 부족 발생 가능
- 로그가 수만 개 이상 쌓일 수 있음

**위치:** `Form1.cs` - `logHistory` 변수

**개선안:**
```csharp
private readonly List<string> logHistory = new List<string>();
private const int MaxLogHistory = 10000; // 최대 10,000개 로그만 유지

private void Log(string message)
{
    string time = DateTime.Now.ToString("HH:mm:ss");
    string logEntry = $"[{time}] {message}";
    
    lock (logHistory) // 스레드 안전성 추가
    {
        logHistory.Add(logEntry);
        
        // 최대 개수 초과 시 오래된 로그 제거
        if (logHistory.Count > MaxLogHistory)
        {
            int removeCount = logHistory.Count - MaxLogHistory;
            logHistory.RemoveRange(0, removeCount);
        }
    }
    
    UpdateLogPreview();
    // ...
}
```

---

### 2. **스레드 안전성: logHistory 동기화 부족**
**문제점:**
- `logHistory`에 여러 스레드가 동시 접근 가능 (UI 스레드, PLC 폴링 스레드, 웹 서버 스레드)
- Race condition 발생 가능

**위치:** `Form1.cs` - 모든 `logHistory` 접근 부분

**개선안:**
- 모든 `logHistory` 접근에 `lock` 추가
- `GetLogsJson()`, `AddLogsToHistory()` 등에도 동기화 필요

---

### 3. **PLC 통신 예외 처리 부족**
**문제점:**
- `PollPlcValues`에서 예외 발생 시 타이머가 계속 실행되어 오류 반복
- PLC 연결 끊김 시 자동 재연결 로직 없음

**위치:** `Form1.cs` - `PollPlcValues()` 메서드

**개선안:**
```csharp
private int consecutiveErrors = 0;
private const int MaxConsecutiveErrors = 5;

private void PollPlcValues(object state)
{
    if (!adsConnected) return;
    
    lock (adsLock)
    {
        try
        {
            // PLC 통신 로직...
            consecutiveErrors = 0; // 성공 시 리셋
        }
        catch (Exception ex)
        {
            consecutiveErrors++;
            if (consecutiveErrors >= MaxConsecutiveErrors)
            {
                // 연속 오류 발생 시 재연결 시도
                BeginInvoke(new Action(() =>
                {
                    Log($"⚠️ PLC 통신 오류 {consecutiveErrors}회 연속 발생. 재연결 시도...");
                    DisconnectFromPlc();
                    if (ethercatPowerOn)
                    {
                        ConnectToPlc();
                    }
                }));
            }
        }
    }
}
```

---

### 4. **웹 서버 스레드 종료 문제**
**문제점:**
- `WebServer.Stop()` 호출 시 `serverThread`가 제대로 종료되지 않을 수 있음
- `HttpListener`가 닫히지 않아 포트가 계속 점유될 수 있음

**위치:** `WebServer.cs` - `Stop()` 메서드

**개선안:**
```csharp
public void Stop()
{
    lock (serverLock)
    {
        if (!isRunning) return;
        
        isRunning = false;
        
        try
        {
            listener?.Stop();
            listener?.Close();
            listener = null;
        }
        catch { }
        
        // 스레드 종료 대기 (최대 5초)
        if (serverThread != null && serverThread.IsAlive)
        {
            if (!serverThread.Join(TimeSpan.FromSeconds(5)))
            {
                serverThread.Abort(); // 강제 종료 (최후의 수단)
            }
            serverThread = null;
        }
    }
}
```

---

## ⚠️ 성능 개선 필요 (Performance Issues)

### 5. **UI 업데이트 과다 호출**
**문제점:**
- `PollPlcValues`에서 매번 `BeginInvoke` 호출
- 센서값이 변경되지 않아도 UI 업데이트 시도
- UI 스레드 부하 증가

**위치:** `Form1.cs` - `PollPlcValues()`, `UpdateSensorsFromPlc()`

**개선안:**
```csharp
private double[] lastSensorValues = new double[SensorCount + 1];
private const double SensorValueThreshold = 0.1; // 0.1 이상 변화 시에만 업데이트

private void UpdateSensorsFromPlc(AnalogInputData data)
{
    // 값이 실제로 변경되었는지 확인
    bool hasChanged = false;
    for (int i = 1; i <= SensorCount; i++)
    {
        double newValue = /* 센서값 계산 */;
        if (Math.Abs(newValue - lastSensorValues[i]) > SensorValueThreshold)
        {
            hasChanged = true;
            lastSensorValues[i] = newValue;
        }
    }
    
    if (hasChanged)
    {
        BeginInvoke(new Action(() => {
            // UI 업데이트
        }));
    }
}
```

---

### 6. **AI 모델 재학습 빈도 과다**
**문제점:**
- `AddTrainingData`에서 100개마다 재학습 → 대량 데이터 추가 시 성능 저하
- 재학습이 UI 스레드를 블로킹할 수 있음

**위치:** `AIModelTrainer.cs` - `AddTrainingData()`

**개선안:**
```csharp
private DateTime lastRetrainTime = DateTime.MinValue;
private readonly TimeSpan retrainInterval = TimeSpan.FromMinutes(5); // 5분마다 재학습

private void AddTrainingData(SensorTrainingData data)
{
    lock (dataLock)
    {
        trainingData.Add(data);
        // ... 데이터 추가 로직
        
        // 주기적으로만 재학습 (100개마다가 아닌 시간 기반)
        if (DateTime.Now - lastRetrainTime > retrainInterval)
        {
            Task.Run(() => RetrainModels()); // 백그라운드 스레드에서 실행
            lastRetrainTime = DateTime.Now;
        }
    }
}
```

---

### 7. **logHistory 검색 성능 저하**
**문제점:**
- `GetLogsJson()`에서 `Where()` 및 `Select()` 연산이 매번 전체 리스트를 순회
- 로그가 많을수록 느려짐

**위치:** `Form1.cs` - `GetLogsJson()`

**개선안:**
```csharp
private string GetLogsJson()
{
    List<object> aiRelevantLogs;
    
    lock (logHistory)
    {
        // 최근 로그만 처리 (뒤에서부터 검색)
        int startIndex = Math.Max(0, logHistory.Count - 1000);
        aiRelevantLogs = logHistory
            .Skip(startIndex) // 최근 1000개만 검색
            .Where(log => /* 필터링 조건 */)
            .Select(log => new { /* ... */ })
            .Take(500)
            .ToList();
    }
    
    return ToJson(aiRelevantLogs);
}
```

---

## 🟡 기능적 개선 필요 (Functional Issues)

### 8. **자동 제어 실제 구현 부재**
**문제점:**
- `TryAutoControl()` 메서드에 PLC 출력 코드가 주석 처리됨
- 실제로 장비를 제어하지 않음

**위치:** `Form1.cs` - `TryAutoControl()`

**개선안:**
```csharp
private void TryAutoControl(int sensorIndex, int alertDirection, string sensorName, 
    double currentValue, double lowerThreshold, double upperThreshold)
{
    // ... 쿨다운 체크 ...
    
    try
    {
        if (adsClient == null || !adsConnected) return;
        
        lock (adsLock)
        {
            // PLC 출력 핀에 실제 제어 신호 전송
            if (sensorIndex == 1 && alertDirection == -1) // 습도 낮음
            {
                // 가습기 제어 (예: Digital Output 핀 5번)
                WriteToPlcOutput(5, true);
                Log($"🤖 자동 제어: 가습기 켜기 (PLC 출력 핀 5)");
            }
            // ... 다른 센서 제어 ...
        }
    }
    catch (Exception ex)
    {
        Log($"⚠️ 자동 제어 실패: {ex.Message}");
    }
}

private void WriteToPlcOutput(int outputPin, bool value)
{
    // PLC 출력 핀에 값 쓰기 (실제 구현 필요)
    // 예: DigitalOutputData 구조체의 해당 비트 설정 후 WriteAny 호출
}
```

---

### 9. **센서값 히스토리 표시 부재**
**문제점:**
- UI에 센서값의 과거 추이를 볼 수 있는 기능 없음
- 그래프는 웹에만 있고 UI에는 없음

**개선안:**
- UI에 간단한 차트 컨트롤 추가 (예: LiveCharts, OxyPlot)
- 또는 센서별 최근 10개 값 표시 테이블 추가

---

### 10. **자동 제어 상태 표시 부재**
**문제점:**
- 자동 제어가 활성화되어 있는지 UI에 명확히 표시되지 않음
- 어떤 센서가 자동 제어되었는지 이력이 없음

**개선안:**
- 자동 제어 상태 표시 레이블 추가
- 자동 제어 이력 로그 표시 (최근 10개)

---

### 11. **실시간 통계 정보 부족**
**문제점:**
- 센서값의 평균, 최소, 최대값을 실시간으로 볼 수 없음
- 일일/주간 통계 없음

**개선안:**
- UI에 통계 패널 추가
- 센서별 최근 1시간/24시간 평균값 표시

---

## 🟢 코드 품질 개선 (Code Quality)

### 12. **예외 처리 일관성 부족**
**문제점:**
- 일부 예외는 무시되고, 일부는 로그만 남김
- 예외 처리 전략이 일관되지 않음

**개선안:**
- 예외 처리 정책 수립
- 중요 예외는 로그 + 사용자 알림
- 경미한 예외는 로그만

---

### 13. **매직 넘버 제거**
**문제점:**
- 하드코딩된 숫자 값들이 많음 (예: 1000, 500, 30 등)

**위치:** 여러 곳

**개선안:**
```csharp
private const int MaxLogHistory = 10000;
private const int MaxLogPreview = 10;
private const int MaxAILogEntries = 500;
private const int SensorPollIntervalMs = 1000;
private const int NotificationCooldownSeconds = 30;
// ...
```

---

### 14. **리소스 해제 보장**
**문제점:**
- 일부 리소스가 `using` 문 없이 사용됨
- 예외 발생 시 리소스가 해제되지 않을 수 있음

**개선안:**
- `IDisposable` 구현 확인
- `using` 문 또는 `try-finally` 사용

---

### 15. **로깅 시스템 개선**
**문제점:**
- 로그 레벨 구분 없음 (INFO, WARNING, ERROR)
- 로그 파일 저장 기능 없음

**개선안:**
```csharp
public enum LogLevel { Info, Warning, Error }

private void Log(string message, LogLevel level = LogLevel.Info)
{
    string prefix = level == LogLevel.Warning ? "⚠️" : 
                   level == LogLevel.Error ? "❌" : "ℹ️";
    // ...
}

// 로그 파일 자동 저장 (일별)
private void SaveLogsToFile()
{
    string fileName = $"Logs_{DateTime.Now:yyyyMMdd}.txt";
    File.AppendAllText(fileName, string.Join("\n", logHistory));
}
```

---

## 📊 UI/UX 개선 필요

### 16. **센서값 변경 애니메이션 부재**
**문제점:**
- 센서값이 급격히 변할 때 시각적 피드백 부족

**개선안:**
- 값 변경 시 색상 변화 애니메이션
- 증가/감소 방향 표시 (↑↓)

---

### 17. **상태 표시 개선**
**문제점:**
- 현재 어떤 센서가 경고 상태인지 한눈에 파악 어려움
- 자동 제어 활성화 여부가 버튼에만 표시됨

**개선안:**
- 상태 대시보드 추가
- 센서별 상태 아이콘 (정상/경고/오류)
- 자동 제어 상태 표시 레이블

---

### 18. **설정 저장/불러오기 기능 부재**
**문제점:**
- 프로그램 재시작 시 설정이 초기화됨
- 농장별 설정이 저장되지 않음

**개선안:**
- 설정 파일 (JSON/XML) 저장
- 프로그램 시작 시 자동 로드

---

## 🔧 아키텍처 개선

### 19. **의존성 주입 부재**
**문제점:**
- 모든 클래스가 직접 인스턴스화됨
- 테스트 어려움

**개선안:**
- 인터페이스 분리 (예: `IPlcCommunicator`, `IAIModelTrainer`)
- 의존성 주입 컨테이너 사용 (선택적)

---

### 20. **단일 책임 원칙 위반**
**문제점:**
- `Form1` 클래스가 너무 많은 책임을 가짐 (UI, PLC 통신, 로깅, 웹 서버 관리)

**개선안:**
- 별도 클래스로 분리:
  - `PlcCommunicator` - PLC 통신 전담
  - `SensorDataManager` - 센서 데이터 관리
  - `NotificationService` - 알림 관리
  - `AutoControlService` - 자동 제어 관리

---

## 📝 우선순위별 개선 계획

### **1단계 (즉시 수정 - 안정성)**
1. ✅ logHistory 메모리 누수 수정
2. ✅ logHistory 스레드 안전성 추가
3. ✅ PLC 통신 예외 처리 강화
4. ✅ 웹 서버 스레드 종료 개선

### **2단계 (단기 - 성능)**
5. ✅ UI 업데이트 최적화
6. ✅ AI 모델 재학습 빈도 조정
7. ✅ 로그 검색 성능 개선

### **3단계 (중기 - 기능)**
8. ✅ 자동 제어 실제 구현
9. ✅ 센서값 히스토리 표시
10. ✅ 자동 제어 상태 표시
11. ✅ 실시간 통계 정보

### **4단계 (장기 - 품질)**
12. ✅ 예외 처리 일관성
13. ✅ 매직 넘버 제거
14. ✅ 리소스 해제 보장
15. ✅ 로깅 시스템 개선
16. ✅ UI/UX 개선
17. ✅ 설정 저장/불러오기
18. ✅ 아키텍처 개선

---

## 🎯 즉시 적용 가능한 핵심 개선사항

가장 중요한 3가지를 우선 수정하는 것을 권장합니다:

1. **logHistory 메모리 관리** - 장기 실행 시 메모리 부족 방지
2. **스레드 안전성** - 멀티스레드 환경에서의 안정성 확보
3. **PLC 통신 예외 처리** - 통신 오류 시 자동 복구

이 3가지만 수정해도 시스템 안정성이 크게 향상됩니다.

