using System.Collections.Generic;

namespace Smart.Agent.Model
{
    public enum SensorType
    {
        Accelerometer,
        StrainGage
    }
    public class Sensor
    {
        public SensorType Type { get; set; }
        public List<SensorData> Data { get; set; }
        public double Average { get; set; }
    }

    public class SensorData
    {
        public byte[] Bytes { get; set; }
        public double Value { get; set; }
    }
}