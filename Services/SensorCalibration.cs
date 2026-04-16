using System;
using SmartFarmUI.Models;

namespace SmartFarmUI.Services
{
    public static class SensorCalibration
    {
        public static int ClampToProgress(double value)
        {
            return (int)Math.Round(Clamp(value, 0, 100));
        }

        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static double ScaleLinear(double raw, double rawMin, double rawMax, double valueMin, double valueMax)
        {
            if (Math.Abs(rawMax - rawMin) < double.Epsilon)
                return valueMin;

            double ratio = (raw - rawMin) / (rawMax - rawMin);
            return valueMin + ratio * (valueMax - valueMin);
        }

        public static double ConvertTemperatureCelsius(int raw)
        {
            const double slope = 0.010256;
            const double intercept = -21.0;
            return slope * raw + intercept;
        }
    }
}
