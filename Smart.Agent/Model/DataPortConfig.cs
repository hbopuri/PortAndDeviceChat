using System;
using System.Collections.Generic;

namespace Smart.Agent.Model
{
    public class DataPortConfig
    {
        public byte[] FirmwareVersion { get; set; }
        public byte[] SampleInterval { get; set; }
        public byte[] PreTriggerSamples { get; set; }
        public byte[] ModeFlag { get; set; }
        public byte[] ModePeriod { get; set; }
        public byte[] ConfigTimestamp { get; set; }
        public DateTime ConfigTimestampUtc { get; set; }
        public byte[] TriggerThreshold { get; set; }
        public byte[] CommandModeTimeout { get; set; }
        public byte[] ActChannelsDataPacking { get; set; }
        public byte[] SleepModeBtOnTime { get; set; }
        public byte[] SleepModeBtOffTime { get; set; }
        public byte[] AmbientTemp { get; set; }
        public byte[] MspTemp { get; set; }
        public byte[] TriggerType { get; set; }
        public byte[] PowerFault { get; set; }

        public List<SensorChannel> SensorChannels { get; set; } = new List<SensorChannel>();

        public byte[] Afe1ModelAndSn { get; set; }
        public byte[] Afe2ModelAndSn { get; set; }
        public byte[] Afe3ModelAndSn { get; set; }
        public byte[] AfeWriteProtect { get; set; }
        public byte[] CompleteResponseBuffer { get; set; }
    }
}