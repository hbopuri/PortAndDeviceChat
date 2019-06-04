using System.Collections.Generic;

namespace Smart.Agent.Model
{
    public class StrainRange
    {
        public const int Min = 1090;
        public const int Max = 1094;
    }
    public class AxRange
    {
        public const int Min = 2046;
        public const int Max = 2050;
    }
    public enum Afe
    {
        Tip,
        Top,
        Mid
    }
    public enum SensorType
    {
        Accelerometer = 0,
        StrainGauge = 1,
        Both = 2
    }
    public class Sensor
    {
        public Afe Afe { get; set; }
        public List<SensorData> Data { get; set; }
        public SensorType Type { get; set; }
    }

    public class SensorData
    {
        //public SensorType Type { get; set; }
        public byte[] Bytes { get; set; }
        //public double Accelerometer { get; set; }
        //public double Strain { get; set; }
        //public byte[] AccelerometerBytes { get; set; }
        //public byte[] StrainBytes { get; set; }
        //public byte[] DataBytes { get; set; }
        public double Value { get; set; }
        //public double AccelerometerValue { get; set; }
        //public double StrainValue { get; set; }
        //public double AccelerometerValueLab { get; set; }
        //public double StrainValueLab { get; set; }
    }

    public enum ChannelMode
    {
        TwoChannel = 21,
        FourChannel = 41,
        SixChannel = 61
    }
}