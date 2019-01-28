namespace Smart.Agent.Model
{
    public class SensorChannel
    {
        public byte[] Active { get; set; }
        public byte[] Type { get; set; }
        public byte[] Gain { get; set; }
        public byte[] Offset { get; set; }
        public byte[] SensitivityGageFactor { get; set; }
        public byte[] AbsoluteR { get; set; }
        public byte[] CalibrationTemp { get; set; }
        public byte[] MonitoringTemp { get; set; }
        public byte[] WriteCount { get; set; }
        public byte[] SamplingFrequency { get; set; }
        public byte[] Location { get; set; }
        public byte[] Distance { get; set; }
        public byte[] Diagnostic { get; set; }
        public byte[] WriteFlagSerialNumber { get; set; }
    }
}