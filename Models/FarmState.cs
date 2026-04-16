namespace SmartFarmUI.Models
{
    public class FarmState
    {
        public int FarmId { get; }
        public SensorThreshold[] Thresholds { get; set; }
        public string CropName { get; set; }
        public string Notes { get; set; }
        public double[] SensorOffsets { get; set; } // 인덱스 0 미사용, 1~4 사용

        public FarmState(int farmId)
        {
            FarmId = farmId;
            Thresholds = new SensorThreshold[SensorConstants.SensorCount + 1];
            for (int i = 0; i <= SensorConstants.SensorCount; i++)
                Thresholds[i] = new SensorThreshold();
            CropName = "";
            Notes = "";
            SensorOffsets = new double[SensorConstants.SensorCount + 1];
        }
    }
}
