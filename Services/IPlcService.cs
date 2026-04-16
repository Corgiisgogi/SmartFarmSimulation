using System;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public interface IPlcService
    {
        event Action<AnalogInputData> AnalogDataReceived;
        event Action<DigitalInputData> DigitalInputReceived;
        event Action<string> ConnectionError;

        bool IsConnected { get; }

        bool Connect(Action<string> log);
        void Disconnect(bool log = true, Action<string> logAction = null);
        void WriteDigitalOutput(DigitalOutputData output);
        void WriteLamp(int index, bool on);
    }
}
