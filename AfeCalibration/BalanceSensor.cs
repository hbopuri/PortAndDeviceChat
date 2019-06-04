using System.Collections.Generic;

namespace AfeCalibration
{
    public class BalanceSensor
    {
        public Channel ChannelOne { get; set; } = new Channel();
        public Channel ChannelTwo { get; set; } = new Channel();
    }
    public class Channel
    {
        public string ChannelName { get; set; }
        public List<double> Reading { get; set; } = new List<double>();
        public bool IsCompleted { get; set; }
    }
}
