using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public class FlaskApiService : IFlaskApiService
    {
        private bool flaskServerRunning = false;

        public bool IsConnected => flaskServerRunning;

        public bool CheckConnection()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"{SensorConstants.FlaskServerUrl}/api/sensors");
                request.Timeout = 3000;
                request.Method = "GET";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string responseText = reader.ReadToEnd();
                    flaskServerRunning = !string.IsNullOrEmpty(responseText);
                    return flaskServerRunning;
                }
            }
            catch
            {
                flaskServerRunning = false;
                return false;
            }
        }

        public void SendSensorData(string sensorDataJson)
        {
            if (!flaskServerRunning) return;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"{SensorConstants.FlaskServerUrl}/api/sensor-data");
                request.Timeout = 2000;
                request.Method = "POST";
                request.ContentType = "application/json";

                byte[] dataBytes = Encoding.UTF8.GetBytes(sensorDataJson);
                request.ContentLength = dataBytes.Length;

                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(dataBytes, 0, dataBytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // 성공적으로 전송됨
                }
            }
            catch (Exception)
            {
                if (flaskServerRunning)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ 센서 데이터 전송 실패");
                }
                flaskServerRunning = false;
            }
        }

        public void SendFarmChange(string sensorDataJson)
        {
            if (!flaskServerRunning) return;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"{SensorConstants.FlaskServerUrl}/api/sensor-data");
                request.Timeout = 2000;
                request.Method = "POST";
                request.ContentType = "application/json";

                byte[] dataBytes = Encoding.UTF8.GetBytes(sensorDataJson);
                request.ContentLength = dataBytes.Length;

                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(dataBytes, 0, dataBytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // 농장 정보 전송 완료
                }
            }
            catch
            {
                // 연결 실패는 조용히 처리
            }
        }

        public async Task<string> RequestAIControlAsync(string jsonData)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"{SensorConstants.FlaskServerUrl}/api/ai/control");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 10000;

            byte[] data = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = data.Length;

            using (Stream requestStream = await request.GetRequestStreamAsync())
            {
                await requestStream.WriteAsync(data, 0, data.Length);
            }

            using (WebResponse response = await request.GetResponseAsync())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public List<string> GetCropList()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"{SensorConstants.FlaskServerUrl}/api/crops");
                request.Method = "GET";
                request.Timeout = 3000;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = response.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            string jsonResponse = reader.ReadToEnd();
                            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

                            if (result != null && result.ContainsKey("crops"))
                            {
                                var crops = JArray.FromObject(result["crops"]);
                                var cropList = new List<string>();

                                foreach (var crop in crops)
                                {
                                    var cropObj = JObject.FromObject(crop);
                                    string cropName = cropObj["name"]?.ToString();
                                    if (!string.IsNullOrEmpty(cropName))
                                    {
                                        cropList.Add(cropName);
                                    }
                                }

                                return cropList;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 연결 실패 시 기본 목록 반환
            }

            return new List<string>
            {
                "사과", "토마토", "상추", "딸기", "오이", "고추", "배추",
                "시금치", "파프리카", "가지", "무", "브로콜리"
            };
        }

        public async Task<Dictionary<string, object>> GetCropInfoAsync(string cropName)
        {
            string encodedCropName = Uri.EscapeDataString(cropName);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"{SensorConstants.FlaskServerUrl}/api/crops/{encodedCropName}");
            request.Method = "GET";
            request.Timeout = 5000;
            request.ContentType = "application/json";

            using (var response = (HttpWebResponse)await Task.Factory.FromAsync(
                request.BeginGetResponse, request.EndGetResponse, null))
            {
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string jsonResponse = await reader.ReadToEndAsync();
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
                    return result;
                }
            }
        }
    }
}
