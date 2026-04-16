using System.Runtime.InteropServices;

namespace SmartFarmUI.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AnalogInputData
    {
        public short Pressure_Sensor;
        public short Vibration_Sensor;
        public short Temperature_Sensor;
        public short Humidity_Sensor;
        public short Reserve4;
        public short Reserve5;
        public short Reserve6;
        public short Reserve7;
    }

    // TwinCAT에서 BIT 타입은 ushort(2바이트)로 패킹됨
    // 16개의 BIT = 2바이트 (16비트)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DigitalInputData
    {
        public ushort Bits; // 비트 0: Button1, 비트 1: Button2, 비트 2: Button3, 비트 3: Button4, ...
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DigitalOutputData
    {
        public ushort Bits; // 비트 0: Lamp1, 비트 1: Lamp2, 비트 2: Lamp3, 비트 3: Lamp4, ...
    }

    public enum EthercatConnectionStatus
    {
        Off,
        Connecting,
        Connected,
        Error
    }
}
