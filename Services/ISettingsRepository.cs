using System.Collections.Generic;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public interface ISettingsRepository
    {
        void SaveThresholds(Dictionary<int, SensorThreshold[]> farmSettings);
        Dictionary<int, SensorThreshold[]> LoadThresholds();
        void SaveFarmInfo(Dictionary<int, string> farmCropNames, Dictionary<int, string> farmNotes);
        void LoadFarmInfo(Dictionary<int, string> farmCropNames, Dictionary<int, string> farmNotes);
    }
}
