﻿using System.Collections.Generic;

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
        public MemsTest MemsTest { get; set; } = new MemsTest();
    }
    public class MemsTest
    {
        public List<TestIteration> TestIteration { get; set; } = new List<TestIteration>();
    }
    public class TestIteration
    {
        public List<double> Reading { get; set; } = new List<double>();
    }
}