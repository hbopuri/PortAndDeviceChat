using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Smart.Agent.Helper;
using Smart.Agent.Model;
using Smart.Log;

namespace Smart.Agent.Business
{
    public class SmartPort
    {
        private SerialPort _port;
        private string _comPortName;
        private Queue _commaQueue;
        private Queue _currentCommand;
        private byte[] _responseBuffer;
        private DataPortConfig _dataPortConfig;
        private Sensor _top;
        private Sensor _tip;
        private byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }

        private void HandleSerialData(byte[] respBuffer)
        {
            _responseBuffer = _responseBuffer == null ? respBuffer : Combine(_responseBuffer, respBuffer);

            if (_responseBuffer.Length != _currentCommand.ExpectedPacketSize) return;
            if (_currentCommand.ExpectedPacketSize == 197) // Read Config Response
                Task.Run(() => ProcessReadConfig(_responseBuffer));
            if (_currentCommand.ExpectedPacketSize == 277) // Collect Response
                Task.Run(() => ProcessSensorData(_responseBuffer));
            byte checkSum;
            if (_currentCommand.ExpectedPacketSize == 277)
            {
                var acknowledgementArray = _responseBuffer.Take(14).ToArray();
                checkSum = acknowledgementArray.Take(acknowledgementArray.Length - 1).ToArray().CheckSum();
                var hexString = acknowledgementArray.ToHex();
                SmartLog.WriteLine(
                    $"Response for {_currentCommand.CommandName} Acknowledgement (CheckSum {new[] {checkSum}.ToHex()} Length:{acknowledgementArray.Length}): {hexString}");

                if (acknowledgementArray[acknowledgementArray.Length - 1] != checkSum)
                {
                    SmartLog.WriteLine(
                        $"*** Invalid Checksum For Collect Acknowledgement Response (CheckSum {new[] {checkSum}.ToHex()}***");
                }

                var sensorDataArray = _responseBuffer.Skip(14).Take(249).ToArray();
                checkSum = sensorDataArray.Take(sensorDataArray.Length - 1).ToArray().CheckSum();
                hexString = sensorDataArray.ToHex();
                SmartLog.WriteLine(
                    $"Response for {_currentCommand.CommandName} Sensor Data (CheckSum {new[] {checkSum}.ToHex()} Length:{sensorDataArray.Length}): {hexString}");

                if (sensorDataArray[sensorDataArray.Length - 1] != checkSum)
                {
                    SmartLog.WriteLine(
                        $"*** Invalid Checksum For Collect Sensor Data Response (CheckSum {new[] {checkSum}.ToHex()}***");
                }

                var completeArray = _responseBuffer.Skip(263).Take(14).ToArray();
                checkSum = completeArray.Take(completeArray.Length - 1).ToArray().CheckSum();
                hexString = completeArray.ToHex();
                SmartLog.WriteLine(
                    $"Response for {_currentCommand.CommandName} Success/Complete (CheckSum {new[] {checkSum}.ToHex()} Length:{completeArray.Length}): {hexString}");

                if (completeArray[completeArray.Length - 1] != checkSum)
                {
                    SmartLog.WriteLine(
                        $"*** Invalid Checksum For Success/Complete Response (CheckSum {new[] {checkSum}.ToHex()}***");
                }
            }
            else
            {
                checkSum = _responseBuffer.Take(_responseBuffer.Length - 1).ToArray().CheckSum();
                if (checkSum != _responseBuffer[_responseBuffer.Length - 1])
                {
                    SmartLog.WriteLine($"*** Invalid Checksum for  {_currentCommand.CommandName} Command Response ***");
                }

                var hexString = _responseBuffer.ToHex();
                SmartLog.WriteLine(
                    $"Response for {_currentCommand.CommandName} (CheckSum {new[] {checkSum}.ToHex()} Length:{_responseBuffer.Length}): {hexString}");
            }



            _responseBuffer = null;

            //SmartLog.WriteLine(ToHex(respBuffer));
            //SmartLog.WriteLine(respBuffer.Length+":"+string.Join("|", respBuffer));
        }

        private void ProcessReadConfig(byte[] responseBuffer)
        {
            var first = responseBuffer.Take(12+22).ToArray().AddAtLast(0x00).AddAtLast(0x00).AddAtLast(0x00).AddAtLast(0x06);
            var second = responseBuffer.Skip(12+22).Take(144).ToArray().AddAtLast(0x00).AddAtLast(0x00).AddAtLast(0x00).AddAtLast(0x04);
            var third = responseBuffer.Skip(12+22+144).Take(responseBuffer.Length - 166).ToArray();
            var finalBytes = Combine(first, second, third);
            SmartLog.WriteLine(finalBytes.ToHex(true));
            var position = 12;
            _dataPortConfig = new DataPortConfig
            {
                FirmwareVersion = finalBytes.Skip(position).Take(2).Reverse().ToArray(),
                SampleInterval = finalBytes.Skip(position+2).Take(2).Reverse().ToArray(),
                PreTriggerSamples = finalBytes.Skip(position+4).Take(1).Reverse().ToArray(),
                ModeFlag = finalBytes.Skip(position+5).Take(1).Reverse().ToArray(),
                ModePeriod = finalBytes.Skip(position+6).Take(2).Reverse().ToArray(),
                ConfigTimestamp = finalBytes.Skip(position+8).Take(4).Reverse().ToArray(),
                TriggerThreshold = finalBytes.Skip(position+12).Take(2).Reverse().ToArray(),
                CommandModeTimeout = finalBytes.Skip(position+14).Take(1).Reverse().ToArray(),
                ActChannelsDataPacking = finalBytes.Skip(position+15).Take(1).Reverse().ToArray(),
                SleepModeBtOnTime = finalBytes.Skip(position+16).Take(1).Reverse().ToArray(),
                SleepModeBtOffTime = finalBytes.Skip(position+17).Take(1).Reverse().ToArray(),
                AmbientTemp = finalBytes.Skip(position+18).Take(1).Reverse().ToArray(),
                MspTemp = finalBytes.Skip(position+19).Take(1).Reverse().ToArray(),
                TriggerType = finalBytes.Skip(position+20).Take(1).Reverse().ToArray(),
                PowerFault = finalBytes.Skip(position+21).Take(1).Reverse().ToArray()
            };
            position = position + 22;
            //_dataPortConfig.Afe1ModelAndSn = responseBuffer.Skip(position).Take(4).Reverse().ToArray();
            //_dataPortConfig.Afe2ModelAndSn = responseBuffer.Skip(position + 4).Take(4).Reverse().ToArray();
            //_dataPortConfig.Afe3ModelAndSn = responseBuffer.Skip(position + 8).Take(4).Reverse().ToArray();
            //_dataPortConfig.AfeWriteProtect = responseBuffer.Skip(position + 12).Take(4).Reverse().ToArray();
            //position = position + 16;
            int channel = 0;

            if (_dataPortConfig.ActChannelsDataPacking.ToHex() == "21")
            {
                channel = 2;
            }
            else if (_dataPortConfig.ActChannelsDataPacking.ToHex() == "41")
            {
                channel = 4;
            }
            else if (_dataPortConfig.ActChannelsDataPacking.ToHex() == "61")
            {
                channel = 6;
            }
           
            for (var i = 0; i < channel; i++)
            {
                //if (i > 0)
                //{
                //    _dataPortConfig.SensorChannels.Add(new SensorChannel
                //    {
                //        Active = responseBuffer.Skip(position).Take(1).Reverse().ToArray(),
                //        Type = responseBuffer.Skip(position + 1).Take(1).Reverse().ToArray(),
                //        Gain = responseBuffer.Skip(position + 2).Take(3).Reverse().ToArray(),
                //        Offset = responseBuffer.Skip(position + 5).Take(4).Reverse().ToArray(),
                //        SensitivityGageFactor = responseBuffer.Skip(position + 9).Take(2).Reverse().ToArray(),
                //        AbsoluteR = responseBuffer.Skip(position + 11).Take(2).Reverse().ToArray(),
                //        CalibrationTemp = responseBuffer.Skip(position + 13).Take(1).Reverse().ToArray(),
                //        MonitoringTemp = responseBuffer.Skip(position + 14).Take(1).Reverse().ToArray(),
                //        WriteCount = responseBuffer.Skip(position + 15).Take(1).Reverse().ToArray(),
                //        SamplingFrequency = responseBuffer.Skip(position + 16).Take(1).Reverse().ToArray(),
                //        Location = responseBuffer.Skip(position + 17).Take(1).Reverse().ToArray(),
                //        Distance = responseBuffer.Skip(position + 18).Take(2).Reverse().ToArray(),
                //        Diagnostic = responseBuffer.Skip(position + 20).Take(2).Reverse().ToArray(),
                //        WriteFlagSerialNumber = responseBuffer.Skip(position + 22).Take(2).Reverse().ToArray()
                //    });
                //    position = position + 24;
                //    _dataPortConfig.SensorChannels[i].Gain = _dataPortConfig.SensorChannels[i].Gain.AddAtLast(00);
                //}
                //else
                //{
                    _dataPortConfig.SensorChannels.Add(new SensorChannel
                    {
                        Active = finalBytes.Skip(position).Take(1).Reverse().ToArray(),
                        Type = finalBytes.Skip(position + 1).Take(1).Reverse().ToArray(),
                        Gain = finalBytes.Skip(position + 2).Take(4).Reverse().ToArray(),
                        Offset = finalBytes.Skip(position + 6).Take(4).Reverse().ToArray(),
                        SensitivityGageFactor = finalBytes.Skip(position + 10).Take(2).Reverse().ToArray(),
                        AbsoluteR = finalBytes.Skip(position + 12).Take(2).Reverse().ToArray(),
                        CalibrationTemp = finalBytes.Skip(position + 14).Take(1).Reverse().ToArray(),
                        MonitoringTemp = finalBytes.Skip(position + 15).Take(1).Reverse().ToArray(),
                        WriteCount = finalBytes.Skip(position + 16).Take(1).Reverse().ToArray(),
                        SamplingFrequency = finalBytes.Skip(position + 17).Take(1).Reverse().ToArray(),
                        Location = finalBytes.Skip(position + 18).Take(1).Reverse().ToArray(),
                        Distance = finalBytes.Skip(position + 19).Take(2).Reverse().ToArray(),
                        Diagnostic = finalBytes.Skip(position + 21).Take(2).Reverse().ToArray(),
                        WriteFlagSerialNumber = finalBytes.Skip(position + 23).Take(2).Reverse().ToArray()
                    });
                    position = position + 25;
                //}
            }

            //position == 173
            _dataPortConfig.Afe1ModelAndSn = finalBytes.Skip(position).Take(4).Reverse().ToArray();
            _dataPortConfig.Afe2ModelAndSn = finalBytes.Skip(position + 4).Take(4).Reverse().ToArray();
            _dataPortConfig.Afe3ModelAndSn = finalBytes.Skip(position + 8).Take(4).Reverse().ToArray();
            _dataPortConfig.AfeWriteProtect = finalBytes.Skip(position + 12).Take(4).Reverse().ToArray();

            SmartLog.WriteLine("-- General and Diagnostics Only ---");
            SmartLog.WriteLine($"Firmware Version: {_dataPortConfig.FirmwareVersion.ToDecimal(0)}");
            SmartLog.WriteLine($"Sample Interval: {_dataPortConfig.SampleInterval.ToDecimal(0)}");
            SmartLog.WriteLine($"PreTrigger Samples: {_dataPortConfig.PreTriggerSamples.ToDecimal(0)}");
            SmartLog.WriteLine($"Mode Flag: {_dataPortConfig.ModeFlag.ToHex()}");
            SmartLog.WriteLine($"Mode Period: {_dataPortConfig.ModePeriod.ToDecimal(0)}");
            SmartLog.WriteLine($"Config Timestamp: {_dataPortConfig.ConfigTimestamp.ToDecimal(0)}");
            SmartLog.WriteLine($"Trigger Threshold: {_dataPortConfig.TriggerThreshold.ToDecimal(0)}");
            SmartLog.WriteLine($"CommandModeTimeout: {_dataPortConfig.CommandModeTimeout.ToDecimal(0)}");
            SmartLog.WriteLine($"ActChannels DataPacking: {_dataPortConfig.ActChannelsDataPacking.ToHex()}");
            SmartLog.WriteLine($"SleepModeBtOnTime: {_dataPortConfig.SleepModeBtOnTime.ToDecimal(0)}");
            SmartLog.WriteLine($"SleepModeBtOffTime: {_dataPortConfig.SleepModeBtOffTime.ToDecimal(0)}");
            SmartLog.WriteLine($"Ambient Temp: {_dataPortConfig.AmbientTemp.ToDecimal(0)}");
            SmartLog.WriteLine($"Msp Temp: {_dataPortConfig.MspTemp.ToDecimal(0)}");
            SmartLog.WriteLine($"Trigger Type: {_dataPortConfig.TriggerType.ToHex()}");
            SmartLog.WriteLine($"Power Fault: {_dataPortConfig.PowerFault.ToBinary()}");
            SmartLog.WriteLine($"Afe1ModelAndSn: {_dataPortConfig.Afe1ModelAndSn.ToHex()}");
            SmartLog.WriteLine($"Afe2ModelAndSn: {_dataPortConfig.Afe2ModelAndSn.ToHex()}");
            SmartLog.WriteLine($"Afe3ModelAndSn: {_dataPortConfig.Afe3ModelAndSn.ToHex()}");
            SmartLog.WriteLine($"AfeWriteProtect: {_dataPortConfig.AfeWriteProtect.ToHex()}");
            int channelIndex = 0;
            foreach (var sensorChannel in _dataPortConfig.SensorChannels)
            {
                SmartLog.WriteLine($"-- Sensor Channel:{channelIndex} ---");
                SmartLog.WriteLine($"Active: {sensorChannel.Active.ToHex()}");
                SmartLog.WriteLine($"Type: {sensorChannel.Type.ToHex()}");
                SmartLog.WriteLine($"Gain: {sensorChannel.Gain.ToFloat(0)}");
                SmartLog.WriteLine($"Offset: {sensorChannel.Offset.ToDecimal(0)}");
                SmartLog.WriteLine($"SensitivityGageFactor: {sensorChannel.SensitivityGageFactor.ToDecimal(0)}");
                SmartLog.WriteLine($"AbsoluteR: {sensorChannel.AbsoluteR.ToDecimal(0)}");
                SmartLog.WriteLine($"CalibrationTemp: {sensorChannel.CalibrationTemp.ToDecimal(0)}");
                SmartLog.WriteLine($"MonitoringTemp: {sensorChannel.MonitoringTemp.ToDecimal(0)}");
                SmartLog.WriteLine($"WriteCount: {sensorChannel.WriteCount.ToDecimal(0)}");
                SmartLog.WriteLine($"SamplingFrequency: {sensorChannel.SamplingFrequency.ToDecimal(0)}");
                SmartLog.WriteLine($"Location: {sensorChannel.Location.ToDecimal(0)}");
                SmartLog.WriteLine($"Distance: {sensorChannel.Distance.ToDecimal(0)}");
                SmartLog.WriteLine($"Diagnostic: {sensorChannel.Diagnostic.ToDecimal(0)}");
                channelIndex++;
            }

        }

        private void ProcessSensorData(byte[] responseBuffer)
        {
            _top = new Sensor
            {
                Type = SensorType.Accelerometer,
                Data = new List<SensorData>()
            };
            _tip = new Sensor
            {
                Type = SensorType.StrainGage,
                Data = new List<SensorData>()
            };

            int position = 22;
            var isUncompressed = _dataPortConfig.ActChannelsDataPacking.ToBitArray()[0];
            switch (_dataPortConfig.ActChannelsDataPacking.ToHex(true))
            {
                case "21":
                {
                        break;
                }
                case "41":
                {
                    if (!isUncompressed)
                    {
                        while (position <= 262)
                        {
                            _top.Data.Add(new SensorData { Bytes = responseBuffer.Skip(position).Take(2).ToArray() });
                            _tip.Data.Add(new SensorData { Bytes = responseBuffer.Skip(position + 2).Take(2).ToArray() });
                            position = position + 4;
                        }
                    }
                    else
                    {
                        while (position <= 262)
                        {
                            _top.Data.Add(new SensorData { Bytes = responseBuffer.Skip(position).Take(3).ToArray() });
                            _tip.Data.Add(new SensorData { Bytes = responseBuffer.Skip(position + 3).Take(3).ToArray() });
                            position = position + 6;
                        }
                    }
                        break;
                }
                case "61":
                {
                    break;
                }
            }
           
            foreach (var t in _top.Data)
            {
                var channelIndex = 0;
                var offset = _dataPortConfig.SensorChannels[channelIndex].Offset.ToDecimal(0);
                var gain = _dataPortConfig.SensorChannels[channelIndex].Gain.ToFloat(0);
                var gage = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor.ToDecimal(0);

                var some = (BitConverter.ToUInt16(t.Bytes, 0) + (double)offset - 2047) * (gain * (0.9039 / (double)gage));
                t.Value = some;
            }

            _top.Average = _top.Data.Average(x => x.Value);

            foreach (var t in _tip.Data)
            {
                var channelIndex = 1;
                var offset = _dataPortConfig.SensorChannels[channelIndex].Offset.ToDecimal(0);
                var gain = _dataPortConfig.SensorChannels[channelIndex].Gain.ToFloat(0);
                var gage = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor.ToDecimal(0);
                var absoluteR = _dataPortConfig.SensorChannels[channelIndex].AbsoluteR.ToDecimal(0);
                t.Value = (BitConverter.ToUInt16(t.Bytes, 0) + (double) offset - 1092 ) * gain * (double) (((decimal) 0.00105026 / (absoluteR / 100)) / gage);
            }

            _tip.Average = _tip.Data.Average(x => x.Value);
           
        }
        private void port_DataReceived(object sender,
            SerialDataReceivedEventArgs e)
        {
            // Show all the incoming data in the port's buffer
            int bytes = _port.BytesToRead;
            //SmartLog.WriteLine($"Trying to Read {bytes} bytes");
            byte[] buffer = new byte[bytes];
            _port.Read(buffer, 0, bytes);
            HandleSerialData(buffer);
            //var response = port.ReadExisting();
            //SmartLog.WriteLine(response);
        }
        public void Init(byte interfaceId, string commPort = "COM6")
        {
            _comPortName = commPort;
            _port = new SerialPort(commPort,
                9600, Parity.None, 8, StopBits.One);
            _port.DataReceived += port_DataReceived;
            _commaQueue = new Queue(interfaceId);
            _commaQueue.LoadLcmQueue();
        }

        public void Start()
        {
            _port.Open();
            SmartLog.WriteLine($"Opened: {_port.PortName}");
            foreach (var queue in _commaQueue.CommandQueue.Where(x => x.SequenceId > 0).OrderBy(x => x.SequenceId))
            {
                ExecuteCommand(queue);
            }

            //SmartLog.WriteLine("--- Completed the Command Queue ---");
        }

        private void ExecuteCommand(Queue queue)
        {
            _currentCommand = queue;
            var command = queue.CommBytes;
            while (queue.MaxRetry >= 1)
            {
                SmartLog.WriteLine($"***** Sending {queue.CommandName} Command *****");
                SmartLog.WriteLine($"Command: {queue.CommBytes.ToHex()}");
                try
                {
                    _port.Write(command, 0, command.Length);
                    if (queue.WaitForNext > 0)
                    {
                        SmartLog.WriteLine($"Sleeping for {queue.WaitForNext} seconds");
                        Thread.Sleep(TimeSpan.FromSeconds(queue.WaitForNext));
                    }

                    queue.MaxRetry = 0;
                }
                catch (Exception exception)
                {
                    SmartLog.WriteLine(exception.Message);
                    if (queue.WaitForNext > 0)
                    {
                        SmartLog.WriteLine($"Sleeping for {queue.RetryWait} seconds before retry");
                        Thread.Sleep(TimeSpan.FromSeconds(queue.RetryWait));
                    }

                    queue.MaxRetry--;
                }
            }
        }

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(_comPortName))
            {
                SmartLog.WriteLine("Port cannot be Null or Empty");
                return;
            }

            using (var port = new SerialWrapper($@"\\.\{_comPortName.ToUpper()}", 9600, SerialWrapper.StopBits.One,
                SerialWrapper.Parity.None))
            {
                if (!port.Open())
                {
                    SmartLog.WriteLine("Unable to connect.");
                    return;
                }
                //port.DataReceived += new
                //    SerialDataReceivedEventHandler(port_DataReceived);
                while (true)
                {
                    Console.Write(port.ReadString(1024));
                }
            }
        }

        public List<Sensor> Collect()
        {
            var collectCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("COLLECT", StringComparison.OrdinalIgnoreCase));
            if(collectCommand != null)
                ExecuteCommand(collectCommand);
            return new List<Sensor>{_top, _tip};
        }
        public void PowerOff()
        {
            var powerOffCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("POWER OFF", StringComparison.OrdinalIgnoreCase));
            if (powerOffCommand != null)
                ExecuteCommand(powerOffCommand);
        }
    }
}