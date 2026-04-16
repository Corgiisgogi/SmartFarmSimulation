namespace SmartFarmUI.Models
{
    public static class SensorConstants
    {
        public const int SensorCount = 4;

        public static readonly string[] DisplayNames = { "", "습도", "온도", "채광", "토양습도" };

        public const int AdsPort = 851;
        public const string AnalogInputSymbol = "GVL.NX_AD4203";
        public const string DigitalInputSymbol = "GVL.NX_ID5342";
        public const string DigitalOutputSymbol = "GVL.NX_OD5121";

        public const string FlaskServerUrl = "http://localhost:5000";

        /// <summary>
        /// Flask AI 제어 명령의 센서 이름을 인덱스로 변환.
        /// 이름에 "토양" 포함 시 4, "습도" 포함 시 1, "온도" 포함 시 2, "채광"/"압력" 포함 시 3.
        /// </summary>
        public static int ResolveSensorIndex(string name, int suggestedIndex)
        {
            if (string.IsNullOrEmpty(name))
                return suggestedIndex;

            // "토양습도"에는 "습도"도 포함되므로 "토양"을 먼저 체크
            if (name.Contains("토양"))
                return 4;
            if (name.Contains("습도"))
                return 1;
            if (name.Contains("온도"))
                return 2;
            if (name.Contains("채광") || name.Contains("압력"))
                return 3;

            return suggestedIndex;
        }

        public static string GetSensorName(int index)
        {
            if (index >= 1 && index <= SensorCount)
                return DisplayNames[index];
            return "";
        }
    }
}
