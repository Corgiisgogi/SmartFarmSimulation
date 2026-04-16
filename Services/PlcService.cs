using System;
using SmartFarmUI.Models;
using TwinCAT.Ads;

namespace SmartFarmUI.Services
{
    public class PlcService : IPlcService
    {
        public event Action<AnalogInputData> AnalogDataReceived;
        public event Action<DigitalInputData> DigitalInputReceived;
        public event Action<string> ConnectionError;

        private TcAdsClient adsClient;
        private int adsAnalogHandle = -1;
        private int adsDigitalInputHandle = -1;
        private int adsDigitalOutputHandle = -1;
        private int[] adsLampHandles = new int[5];
        private System.Threading.Timer adsPollTimer;
        private readonly object adsLock = new object();
        private bool adsConnected = false;

        public bool IsConnected => adsConnected;

        public bool Connect(Action<string> log)
        {
            Disconnect(false);

            try
            {
                adsClient = new TcAdsClient();
                adsClient.Connect(SensorConstants.AdsPort);
                adsAnalogHandle = adsClient.CreateVariableHandle(SensorConstants.AnalogInputSymbol);

                // 디지털 입력 핸들 생성
                string[] inputSymbols = { SensorConstants.DigitalInputSymbol, "NX_ID5342" };
                foreach (string symbol in inputSymbols)
                {
                    try
                    {
                        adsDigitalInputHandle = adsClient.CreateVariableHandle(symbol);
                        log?.Invoke($"디지털 입력 핸들 생성 성공: {symbol}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (symbol == inputSymbols[inputSymbols.Length - 1])
                            log?.Invoke($"⚠️ 디지털 입력 핸들 생성 실패: {ex.Message} (버튼 기능 비활성화)");
                    }
                }

                // 디지털 출력 핸들 생성
                string[] outputSymbols = { SensorConstants.DigitalOutputSymbol, "NX_OD5121" };
                foreach (string symbol in outputSymbols)
                {
                    try
                    {
                        adsDigitalOutputHandle = adsClient.CreateVariableHandle(symbol);
                        log?.Invoke($"디지털 출력 핸들 생성 성공: {symbol}");

                        try
                        {
                            string baseSymbol = symbol.Contains(".") ? symbol.Substring(0, symbol.LastIndexOf('.')) : "";
                            if (!string.IsNullOrEmpty(baseSymbol))
                            {
                                adsLampHandles[1] = adsClient.CreateVariableHandle($"{baseSymbol}.Lamp1");
                                adsLampHandles[2] = adsClient.CreateVariableHandle($"{baseSymbol}.Lamp2");
                                adsLampHandles[3] = adsClient.CreateVariableHandle($"{baseSymbol}.Lamp3");
                                adsLampHandles[4] = adsClient.CreateVariableHandle($"{baseSymbol}.Lamp4");
                                log?.Invoke("개별 램프 핸들 생성 성공");
                            }
                        }
                        catch { }
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (symbol == outputSymbols[outputSymbols.Length - 1])
                            log?.Invoke($"⚠️ 디지털 출력 핸들 생성 실패: {ex.Message} (램프 기능 비활성화)");
                    }
                }

                adsPollTimer = new System.Threading.Timer(PollPlcValues, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000));
                adsConnected = true;

                log?.Invoke("PLC 연결 성공");
                return true;
            }
            catch (Exception ex)
            {
                Disconnect(false);
                throw new Exception($"PLC 연결 실패: {ex.Message}", ex);
            }
        }

        public void Disconnect(bool log = true, Action<string> logAction = null)
        {
            lock (adsLock)
            {
                adsPollTimer?.Dispose();
                adsPollTimer = null;

                if (adsClient != null)
                {
                    try
                    {
                        if (adsAnalogHandle != -1)
                        {
                            adsClient.DeleteVariableHandle(adsAnalogHandle);
                            adsAnalogHandle = -1;
                        }
                        if (adsDigitalInputHandle != -1)
                        {
                            adsClient.DeleteVariableHandle(adsDigitalInputHandle);
                            adsDigitalInputHandle = -1;
                        }
                        if (adsDigitalOutputHandle != -1)
                        {
                            adsClient.DeleteVariableHandle(adsDigitalOutputHandle);
                            adsDigitalOutputHandle = -1;
                        }
                        for (int i = 1; i <= 4; i++)
                        {
                            if (adsLampHandles[i] != -1)
                            {
                                try { adsClient.DeleteVariableHandle(adsLampHandles[i]); } catch { }
                                adsLampHandles[i] = -1;
                            }
                        }
                        adsClient.Dispose();
                    }
                    catch { }
                    finally
                    {
                        adsClient = null;
                    }
                }

                adsConnected = false;
            }

            if (log)
                logAction?.Invoke("PLC 연결 해제");
        }

        public void WriteDigitalOutput(DigitalOutputData output)
        {
            lock (adsLock)
            {
                if (adsClient == null || !adsConnected || adsDigitalOutputHandle == -1)
                    return;

                adsClient.WriteAny(adsDigitalOutputHandle, output);
            }
        }

        public void WriteLamp(int index, bool on)
        {
            lock (adsLock)
            {
                if (adsClient == null || !adsConnected)
                    return;

                if (index >= 1 && index <= 4 && adsLampHandles[index] != 0)
                {
                    try
                    {
                        adsClient.WriteAny(adsLampHandles[index], on);
                    }
                    catch { }
                }
            }
        }

        private void PollPlcValues(object state)
        {
            lock (adsLock)
            {
                if (adsClient == null || !adsConnected || adsAnalogHandle == -1)
                    return;

                try
                {
                    var analogData = (AnalogInputData)adsClient.ReadAny(adsAnalogHandle, typeof(AnalogInputData));
                    AnalogDataReceived?.Invoke(analogData);

                    if (adsDigitalInputHandle != -1)
                    {
                        try
                        {
                            var digitalInput = (DigitalInputData)adsClient.ReadAny(adsDigitalInputHandle, typeof(DigitalInputData));
                            DigitalInputReceived?.Invoke(digitalInput);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    adsConnected = false;
                    ConnectionError?.Invoke(ex.Message);
                }
            }
        }
    }
}
