using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartFarmUI.Services
{
    public interface IFlaskApiService
    {
        bool IsConnected { get; }

        bool CheckConnection();
        void SendSensorData(string sensorDataJson);
        void SendFarmChange(string sensorDataJson);
        Task<string> RequestAIControlAsync(string jsonData);
        List<string> GetCropList();
        Task<Dictionary<string, object>> GetCropInfoAsync(string cropName);
    }
}
