using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Smart.Agent.Model;
using Smart.Hid;
using Smart.Log;
using Smart.Log.Helper;
using Queue = Smart.Agent.Model.Queue;

namespace Smart.Agent.Business
{
    public class SmartPort
    {
        private SerialPort _port;
        private Queue _commaQueue;
        private Queue _currentCommand;
        private byte[] _responseBuffer;
        private DataPortConfig _dataPortConfig;
        private List<Sensor> _sensors;
        private byte _boardId;
        private string _comPortName;

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
            {
                var readResponse = _responseBuffer;
                Task.Run(() => ProcessReadConfig(readResponse));
            }

            if (_currentCommand.ExpectedPacketSize == 277) // Collect Response
            {
                var collectResponse = _responseBuffer;
                Task.Run(() => ProcessSensorData(collectResponse));
            }

            byte checksum;
            if (_currentCommand.ExpectedPacketSize == 277)
            {
                var tempBuffer = _responseBuffer;
                var acknowledgementArray = tempBuffer.Take(14).ToArray();
                checksum = acknowledgementArray.Take(acknowledgementArray.Length - 1).ToArray().CheckSum();
                //var hexString = acknowledgementArray.ToHex();
                //SmartLog.WriteLine(
                //    $"\tResponse for {_currentCommand.CommandName} Acknowledgement (Checksum {new[] {checksum}.ToHex()} Length:{acknowledgementArray.Length}): {hexString}");

                if (acknowledgementArray[acknowledgementArray.Length - 1] != checksum)
                {
                    SmartLog.WriteLine(
                        $"*** Invalid Checksum For Collect Acknowledgement Response (CheckSum {new[] {checksum}.ToHex()}***");
                }

                var sensorDataArray = tempBuffer.Skip(14).Take(249).ToArray();
                checksum = sensorDataArray.Take(sensorDataArray.Length - 1).ToArray().CheckSum();
                //hexString = sensorDataArray.ToHex();
                //SmartLog.WriteLine(
                //    $"\tResponse for {_currentCommand.CommandName} Sensor Data (Checksum {new[] {checksum}.ToHex()} Length:{sensorDataArray.Length}): {hexString}");

                if (sensorDataArray[sensorDataArray.Length - 1] != checksum)
                {
                    SmartLog.WriteLine(
                        $"*** Invalid Checksum For Collect Sensor Data Response (Checksum {new[] {checksum}.ToHex()}***");
                }

                var completeArray = tempBuffer.Skip(263).Take(14).ToArray();
                checksum = completeArray.Take(completeArray.Length - 1).ToArray().CheckSum();
                //hexString = completeArray.ToHex();
                //SmartLog.WriteLine(
                //    $"\tResponse for {_currentCommand.CommandName} Success/Complete (Checksum {new[] {checksum}.ToHex()} Length:{completeArray.Length}): {hexString}");

                if (completeArray[completeArray.Length - 1] != checksum)
                {
                    SmartLog.WriteLine(
                        $"*** Invalid Checksum For Success/Complete Response (Checksum {new[] {checksum}.ToHex()}***");
                }
            }
            else
            {
                checksum = _responseBuffer.Take(_responseBuffer.Length - 1).ToArray().CheckSum();
                if (checksum != _responseBuffer[_responseBuffer.Length - 1])
                {
                    SmartLog.WriteLine($"*** Invalid Checksum for  {_currentCommand.CommandName} Command Response ***");
                }

                //var hexString = _responseBuffer.ToHex();
                //SmartLog.WriteLine(
                //    $"\tResponse for {_currentCommand.CommandName} (CheckSum {new[] {checksum}.ToHex()} Length:{_responseBuffer.Length}): {hexString}");
            }

            SmartLog.WriteLine($"\tResponse Buffer Length: {_responseBuffer.Length}");
        }

        private void ProcessReadConfig(byte[] responseBuffer)
        {
            var position = 12;
            _dataPortConfig = new DataPortConfig
            {
                FirmwareVersion = responseBuffer.Skip(position).Take(2).Reverse().ToArray(),
                SampleInterval = responseBuffer.Skip(position + 2).Take(2).Reverse().ToArray(),
                PreTriggerSamples = responseBuffer.Skip(position + 4).Take(1).ToArray(),
                ModeFlag = responseBuffer.Skip(position + 5).Take(1).ToArray(),
                ModePeriod = responseBuffer.Skip(position + 6).Take(2).Reverse().ToArray(),
                ConfigTimestamp = responseBuffer.Skip(position + 8).Take(4).Reverse().ToArray(),
                TriggerThreshold = responseBuffer.Skip(position + 12).Take(2).Reverse().ToArray(),
                CommandModeTimeout = responseBuffer.Skip(position + 14).Take(1).ToArray(),
                ActChannelsDataPacking = responseBuffer.Skip(position + 15).Take(1).ToArray(),
                SleepModeBtOnTime = responseBuffer.Skip(position + 16).Take(1).ToArray(),
                SleepModeBtOffTime = responseBuffer.Skip(position + 17).Take(1).ToArray(),
                AmbientTemp = responseBuffer.Skip(position + 18).Take(1).ToArray(),
                MspTemp = responseBuffer.Skip(position + 19).Take(1).ToArray(),
                TriggerType = responseBuffer.Skip(position + 20).Take(1).ToArray(),
                PowerFault = responseBuffer.Skip(position + 21).Take(1).ToArray()
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
                _dataPortConfig.SensorChannels.Add(new SensorChannel
                {
                    Active = responseBuffer.Skip(position).Take(1).ToArray(),
                    Type = responseBuffer.Skip(position + 1).Take(1).ToArray(),
                    Gain = responseBuffer.Skip(position + 2).Take(4).Reverse().ToArray(),
                    Offset = responseBuffer.Skip(position + 6).Take(4).Reverse().ToArray(),
                    SensitivityGageFactor = responseBuffer.Skip(position + 10).Take(2).Reverse().ToArray(),
                    AbsoluteR = responseBuffer.Skip(position + 12).Take(2).Reverse().ToArray(),
                    CalibrationTemp = responseBuffer.Skip(position + 14).Take(1).ToArray(),
                    MonitoringTemp = responseBuffer.Skip(position + 15).Take(1).ToArray(),
                    WriteCount = responseBuffer.Skip(position + 16).Take(1).ToArray(),
                    SamplingFrequency = responseBuffer.Skip(position + 17).Take(1).ToArray(),
                    Location = responseBuffer.Skip(position + 18).Take(1).ToArray(),
                    Distance = responseBuffer.Skip(position + 19).Take(2).Reverse().ToArray(),
                    Diagnostic = responseBuffer.Skip(position + 21).Take(1).Reverse().ToArray(),
                    WriteFlagSerialNumber = responseBuffer.Skip(position + 22).Take(2).Reverse().ToArray()
                });
                position = position + 24;
            }

            //position == 173
            _dataPortConfig.Afe1ModelAndSn = responseBuffer.Skip(position).Take(4).ToArray();
            _dataPortConfig.Afe2ModelAndSn = responseBuffer.Skip(position + 4).Take(4).ToArray();
            _dataPortConfig.Afe3ModelAndSn = responseBuffer.Skip(position + 8).Take(4).ToArray();
            _dataPortConfig.AfeWriteProtect = responseBuffer.Skip(position + 12).Take(4).ToArray();

            SmartLog.WriteLine("-- General and Diagnostics Only ---");
            SmartLog.WriteLine($"\tFirmware Version: {_dataPortConfig.FirmwareVersion.ToDecimal(0)}");
            SmartLog.WriteLine($"\tSample Interval: {_dataPortConfig.SampleInterval.ToDecimal(0)}");
            SmartLog.WriteLine($"\tPreTrigger Samples: {_dataPortConfig.PreTriggerSamples.ToDecimal(0)}");
            SmartLog.WriteLine($"\tMode Flag: {_dataPortConfig.ModeFlag.ToHex()}");
            SmartLog.WriteLine($"\tMode Period: {_dataPortConfig.ModePeriod.ToDecimal(0)}");
            SmartLog.WriteLine($"\tConfig Timestamp: {_dataPortConfig.ConfigTimestamp.ToDecimal(0)}");
            SmartLog.WriteLine($"\tTrigger Threshold: {_dataPortConfig.TriggerThreshold.ToDecimal(0)}");
            SmartLog.WriteLine($"\tCommandModeTimeout: {_dataPortConfig.CommandModeTimeout.ToDecimal(0)}");
            SmartLog.WriteLine($"\tActChannels DataPacking: {_dataPortConfig.ActChannelsDataPacking.ToHex()}");
            SmartLog.WriteLine($"\tSleepModeBtOnTime: {_dataPortConfig.SleepModeBtOnTime.ToDecimal(0)}");
            SmartLog.WriteLine($"\tSleepModeBtOffTime: {_dataPortConfig.SleepModeBtOffTime.ToDecimal(0)}");
            SmartLog.WriteLine($"\tAmbient Temp: {_dataPortConfig.AmbientTemp.ToDecimal(0)}");
            SmartLog.WriteLine($"\tMsp Temp: {_dataPortConfig.MspTemp.ToDecimal(0)}");
            SmartLog.WriteLine($"\tTrigger Type: {_dataPortConfig.TriggerType.ToHex()}");
            SmartLog.WriteLine($"\tPower Fault: {_dataPortConfig.PowerFault.ToBinary()}");
            SmartLog.WriteLine($"\tAfe1ModelAndSn: {_dataPortConfig.Afe1ModelAndSn.ToHex()}");
            SmartLog.WriteLine($"\tAfe2ModelAndSn: {_dataPortConfig.Afe2ModelAndSn.ToHex()}");
            SmartLog.WriteLine($"\tAfe3ModelAndSn: {_dataPortConfig.Afe3ModelAndSn.ToHex()}");
            SmartLog.WriteLine($"\tAfeWriteProtect: {_dataPortConfig.AfeWriteProtect.ToHex()}");
            int channelIndex = 0;
            foreach (var sensorChannel in _dataPortConfig.SensorChannels)
            {
                SmartLog.WriteLine($"-- Sensor Channel:{channelIndex} ---");
                SmartLog.WriteLine($"\tActive: {sensorChannel.Active.ToHex()}");
                SmartLog.WriteLine($"\tType: {sensorChannel.Type.ToHex()}");
                SmartLog.WriteLine($"\tGain: {sensorChannel.Gain.ToFloat(0)}");
                SmartLog.WriteLine($"\tOffset: {sensorChannel.Offset.ToDecimal(0)}");
                SmartLog.WriteLine($"\tSensitivityGageFactor: {sensorChannel.SensitivityGageFactor.ToDecimal(0)}");
                SmartLog.WriteLine($"\tAbsoluteR: {sensorChannel.AbsoluteR.ToDecimal(0)}");
                SmartLog.WriteLine($"\tCalibrationTemp: {sensorChannel.CalibrationTemp.ToDecimal(0)}");
                SmartLog.WriteLine($"\tMonitoringTemp: {sensorChannel.MonitoringTemp.ToDecimal(0)}");
                SmartLog.WriteLine($"\tWriteCount: {sensorChannel.WriteCount.ToDecimal(0)}");
                SmartLog.WriteLine($"\tSamplingFrequency: {sensorChannel.SamplingFrequency.ToDecimal(0)}");
                SmartLog.WriteLine($"\tLocation: {sensorChannel.Location.ToDecimal(0)}");
                SmartLog.WriteLine($"\tDistance: {sensorChannel.Distance.ToDecimal(0)}");
                SmartLog.WriteLine($"\tDiagnostic: {sensorChannel.Diagnostic.ToDecimal(0)}");
                channelIndex++;
            }

        }

        private void ProcessSensorData(byte[] responseBuffer)
        {
            _sensors = new List<Sensor>();
            foreach (var channel in _dataPortConfig.SensorChannels)
            {
                #region Top-Tip-Mid Logic Documetation

                /*
                 * HaBo had a call with Rich S on 01/30/2019 anc confirmed the following
                 * To keep it simple we only want to support top and tip
                 * And we expect users will only operate with top, tip will be edge case scenario
                 * 0-1 top
                 * 2-3 tip/top (mostly tip)
                 * 4-5 mid
                 *
                 */

                #endregion

                switch (channel.Type.ToDecimal(0))
                {
                    case (int) SensorType.Accelerometer:
                        _sensors.Add(new Sensor
                        {
                            Afe = new List<int> {0, 1}.Any(x => _dataPortConfig.SensorChannels.IndexOf(channel) == x)
                                ? Afe.Top
                                : new List<int> {2, 3}.Any(x => _dataPortConfig.SensorChannels.IndexOf(channel) == x)
                                    ? Afe.Tip
                                    : Afe.Mid,
                            Data = new List<SensorData>()
                        });
                        break;
                    case (int) SensorType.StrainGage:
                        _sensors.Add(new Sensor
                        {
                            Afe = new List<int> {0, 1}.Any(x => _dataPortConfig.SensorChannels.IndexOf(channel) == x)
                                ? Afe.Top
                                : new List<int> {2, 3}.Any(x => _dataPortConfig.SensorChannels.IndexOf(channel) == x)
                                    ? Afe.Tip
                                    : Afe.Mid,
                            Data = new List<SensorData>()
                        });
                        break;
                }
            }

            int position = 22;
            var isCompressed = _dataPortConfig.ActChannelsDataPacking.ToBitArray()[0];
            switch (Convert.ToInt32(_dataPortConfig.ActChannelsDataPacking.ToHex(true)))
            {
                case (int) ChannelMode.TwoChannel:
                {
                    break;
                }
                case (int) ChannelMode.FourChannel:
                {
                    if (isCompressed)
                    {
                        while (position < 262)
                        {
                            var currentThree = responseBuffer.Skip(position).Take(3).ToArray();
                            GetAccAndStrainInDouble(currentThree, out var accelerometer, out var strain);
                            GetAccAndStrainInBytes(currentThree, out var accelerometerBytes, out var strainBytes);
                            _sensors.First(x => x.Afe == Afe.Top).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = currentThree,
                                        Accelerometer = accelerometer,
                                        Strain = strain,
                                        AccelerometerBytes = accelerometerBytes,
                                        StrainBytes = strainBytes
                                    });

                            currentThree = responseBuffer.Skip(position + 3).Take(3).ToArray();
                            GetAccAndStrainInDouble(currentThree, out accelerometer, out strain);
                            GetAccAndStrainInBytes(currentThree, out accelerometerBytes, out strainBytes);
                            _sensors.First(x => x.Afe == Afe.Tip).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = currentThree,
                                        Accelerometer = accelerometer,
                                        Strain = strain,
                                        AccelerometerBytes = accelerometerBytes,
                                        StrainBytes = strainBytes
                                    });

                            currentThree = responseBuffer.Skip(position + 6).Take(3).ToArray();
                            GetAccAndStrainInDouble(currentThree, out accelerometer, out strain);
                            GetAccAndStrainInBytes(currentThree, out accelerometerBytes, out strainBytes);
                            _sensors.First(x => x.Afe == Afe.Top).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = currentThree,
                                        Accelerometer = accelerometer,
                                        Strain = strain,
                                        AccelerometerBytes = accelerometerBytes,
                                        StrainBytes = strainBytes
                                    });

                            currentThree = responseBuffer.Skip(position + 9).Take(3).ToArray();
                            GetAccAndStrainInDouble(currentThree, out accelerometer, out strain);
                            GetAccAndStrainInBytes(currentThree, out accelerometerBytes, out strainBytes);
                            _sensors.First(x => x.Afe == Afe.Tip).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = currentThree,
                                        Accelerometer = accelerometer,
                                        Strain = strain,
                                        AccelerometerBytes = accelerometerBytes,
                                        StrainBytes = strainBytes
                                    });

                            position = position + 12;
                        }
                    }
                    else
                    {
                        while (position < 262)
                        {
                            _sensors.First(x => x.Afe == Afe.Top).Data.Add(
                                new SensorData
                                    {Bytes = responseBuffer.Skip(position).Take(2).ToArray()});
                            _sensors.First(x => x.Afe == Afe.Top).Data.Add(
                                new SensorData {Bytes = responseBuffer.Skip(position + 2).Take(2).ToArray()});
                            _sensors.First(x => x.Afe == Afe.Tip).Data.Add(
                                new SensorData
                                    {Bytes = responseBuffer.Skip(position + 4).Take(2).ToArray()});
                            _sensors.First(x => x.Afe == Afe.Tip).Data.Add(
                                new SensorData
                                    {Bytes = responseBuffer.Skip(position + 6).Take(2).ToArray()});
                            position = position + 8;
                        }
                    }

                    break;
                }
                case (int) ChannelMode.SixChannel:
                {
                    break;
                }
            }

            foreach (var sensor in _sensors)
            {
                switch (sensor.Afe)
                {
                    case Afe.Top:
                        if (sensor.Data.Any(x => x.AccelerometerBytes != null))
                            CalculateAccelerometer(sensor, 0);
                        if (sensor.Data.Any(x => x.StrainBytes != null))
                            CalculateStrain(sensor, 1);
                        break;
                    case Afe.Tip:
                        if (sensor.Data.Any(x => x.AccelerometerBytes != null))
                            CalculateAccelerometer(sensor, 2);
                        if (sensor.Data.Any(x => x.StrainBytes != null))
                            CalculateStrain(sensor, 3);
                        break;
                    case Afe.Mid:
                        if (sensor.Data.Any(x => x.AccelerometerBytes != null))
                            CalculateAccelerometer(sensor, 4);
                        if (sensor.Data.Any(x => x.StrainBytes != null))
                            CalculateStrain(sensor, 5);
                        break;
                }
            }
        }

        private void CalculateStrain(Sensor sensor, int channelIndex)
        {
            foreach (var t in sensor.Data.Where(x => x.StrainBytes != null))
            {
                //var channelIndex = 1;
                var offset = _dataPortConfig.SensorChannels[channelIndex].Offset.ToDecimal(0);
                var gain = _dataPortConfig.SensorChannels[channelIndex].Gain.ToFloat(0);
                var gage = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor.ToDecimal(0);
                var absoluteR = _dataPortConfig.SensorChannels[channelIndex].AbsoluteR.ToDecimal(0);
                var n = t.StrainBytes.ToArray();
                var someA = (BitConverter.ToUInt16(n, 0) + (double) offset - 1092) * gain *
                            (double) (((decimal) 0.00105026 / (absoluteR / 100)) / (gage / 1000));
                if (_dataPortConfig.SensorChannels[channelIndex].Type.ToDecimal(0) == 1)
                {
                    someA = someA * 1000000;
                }

                t.StrainValue = someA;

                var someB = (t.Strain + (double) offset - 1092) * gain *
                            (double) (((decimal) 0.00105026 / (absoluteR / 100)) / (gage / 1000));
                if (_dataPortConfig.SensorChannels[channelIndex].Type.ToDecimal(0) == 1)
                {
                    someB = someB * 1000000;
                }

                t.StrainValueLab = someB;
            }
        }

        private void CalculateAccelerometer(Sensor sensor, int channelIndex)
        {
            foreach (var t in sensor.Data.Where(x => x.AccelerometerBytes != null))
            {
                //var channelIndex = 0;
                var offset = _dataPortConfig.SensorChannels[channelIndex].Offset.ToDecimal(0);
                var gain = _dataPortConfig.SensorChannels[channelIndex].Gain.ToFloat(0);
                var gage = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor.ToDecimal(0);
                var n = t.AccelerometerBytes.ToArray();
                var someA = (((BitConverter.ToUInt16(n, 0) + (double) offset) - 2047) * gain) *
                            (0.9039 / (double) (gage / 1000));

                if (_dataPortConfig.SensorChannels[channelIndex].Type.ToDecimal(0) == 1)
                {
                    someA = someA * 1000000;
                }

                t.AccelerometerValue = someA;

                var someB = (((t.Accelerometer + (double) offset) - 2047) * gain) *
                            (0.9039 / (double) (gage / 1000));

                if (_dataPortConfig.SensorChannels[channelIndex].Type.ToDecimal(0) == 1)
                {
                    someB = someB * 1000000;
                }

                t.AccelerometerValueLab = someB;
            }
        }

        private static void GetAccAndStrainInDouble(byte[] currentThree, out double accelerometer, out double strain)
        {
            var first = currentThree.ToBinary().Split('-')[0];
            //var firstLeft = first.Substring(4, 4)+ "0000";

            var second = currentThree.ToBinary().Split('-')[1];
            var secondLeft = second.Substring(4, 4) + "0000";

            var secondRightFromLeft = secondLeft.Substring(0, 4);
            secondRightFromLeft = "0000" + secondRightFromLeft;


            var high = Convert.ToInt32(secondRightFromLeft, 2);
            var low = Convert.ToInt32(first, 2);
            accelerometer = high * Math.Pow(2, 8) + low;

            var secondRight = "0000" + second.Substring(0, 4);
            var third = currentThree.ToBinary().Split('-')[2];
            var thirdLeft = third.Substring(4, 4) + "0000";
            var thirdRight = "0000" + third.Substring(0, 4);

            low = Convert.ToInt32(secondRight, 2) + Convert.ToInt32(thirdLeft, 2);
            high = Convert.ToInt32(thirdRight, 2);
            strain = high * Math.Pow(2, 8) + low;
        }

        private static void GetAccAndStrainInBytes(byte[] currentThree, out byte[] accelerometer, out byte[] strain)
        {
            //var firstLowerNibble = currentThree[0].GetNibble(0);
            //var firstUpperNibble = currentThree[0].GetNibble(1);
            //var secondLowerNibble = currentThree[1].GetNibble(2);
            //var secondUpperNibble = currentThree[1].GetNibble(0);

            //var thirdLowerNibble = currentThree[2].GetNibble(1);
            //var thirdUpperNibble = currentThree[2].GetNibble(2);

            //accelerometer = new byte[] {firstLowerNibble, firstUpperNibble, secondLowerNibble, 0x00};
            //strain = new byte[] {secondUpperNibble, thirdLowerNibble, thirdUpperNibble, 0x00};

            int low = BitConverter.ToInt32(new byte[] {currentThree[0], currentThree[1], 0, 0}, 0);
            int high = BitConverter.ToInt32(new byte[] {currentThree[1], currentThree[2], 0, 0}, 0);

            low = low & 0x0fff;
            high = high & 0xfff0;
            high = high << 12;
            int all = high | low;

            byte[] intBytes = BitConverter.GetBytes(all);
            accelerometer = intBytes.Take(2).ToArray();
            strain = intBytes.Skip(2).Take(2).ToArray();
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

        public void Init(byte interfaceId, string commPort)
        {
            _boardId = interfaceId;
            _comPortName = commPort;
            _port = new SerialPort(commPort,
                9600, Parity.None, 8, StopBits.One);
            _port.DataReceived += port_DataReceived;
            _commaQueue = new Queue(interfaceId);
            _commaQueue.LoadLcmQueue();
        }

        public void Start()
        {
            if (!_port.IsOpen)
                _port.Open();
            SmartLog.WriteLine($"Opened: {_port.PortName}");
            foreach (var queue in _commaQueue.CommandQueue.Where(x => x.SequenceId > 0).OrderBy(x => x.SequenceId))
            {
                ExecuteCommand(queue);
            }
        }
        public void ClosePort()
        {
            if (_port.IsOpen)
                _port.Close();
            SmartLog.WriteLine($"Closed: {_port.PortName}");
        }

        private void ExecuteCommand(Queue queue)
        {
            _responseBuffer = null;
            _currentCommand = queue;
            var command = queue.CommBytes;
            var maxRetry = queue.MaxRetry;
            while (maxRetry >= 1)
            {
                //SmartLog.WriteLine();
                SmartLog.WriteLine($"***** Sending {queue.CommandName} Command *****");
                SmartLog.WriteLine($"\tCommand: {queue.CommBytes.ToHex()}");
                try
                {
                    _port.Write(command, 0, command.Length);
                    if (queue.WaitForNext > 0)
                    {
                        SmartLog.WriteLine($"\tSleeping for {queue.WaitForNext} seconds");
                        Thread.Sleep(TimeSpan.FromSeconds(queue.WaitForNext));
                    }

                    maxRetry = 0;
                }
                catch (Exception exception)
                {
                    SmartLog.WriteLine(exception.Message);
                    if (queue.WaitForNext > 0)
                    {
                        SmartLog.WriteLine($"\tSleeping for {queue.RetryWait} seconds before retry");
                        Thread.Sleep(TimeSpan.FromSeconds(queue.RetryWait));
                    }

                    maxRetry--;
                }
            }
        }

        public async Task<LoopResponse> Go(int menuOption)
        {
            SmartLog.WriteLine();
            var loopResponse = new LoopResponse {Selection = menuOption};
            switch (menuOption)
            {
                case 1:
                    try
                    {
                        //if (!_port.IsOpen)
                        //{
                        //    Init(_boardId, _comPortName);
                        //    Start();
                        //}
                        loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case 2:
                    try
                    {
                        ReadDataPortConfig();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case 3:
                    try
                    {

                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case 4:
                    try
                    {
                        Start();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case 5:
                    try
                    {
                        //PowerOff();
                        //ClosePort();
                        //await SmartDevice.IncrementAsync();
                        //await UsbToSpiConverter.Init();
                        await UsbToSpiConverter.Increment();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case 6:
                    try
                    {
                        ClosePort();
                        await SmartDevice.DecrementAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case 7:
                    try
                    {
                        ClosePort();
                        await SmartDevice.SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
            }

            return await Task.FromResult(loopResponse);
        }

        public int Menu()
        {
            while (true)
            {
                //Console.Clear();
                SmartLog.WriteLine("\nSerial Port Menu");
                SmartLog.WriteLine("--------------------------------------------------------");
                //SmartLog.WriteLine();
                SmartLog.WriteLine("1. Issue COLLECT Command");
                SmartLog.WriteLine("2. Issue READ DATA PORT CONFIG Command");
                //SmartLog.WriteLine("3. Move Next");
                SmartLog.WriteLine("4. Start EDC All Over");
                SmartLog.WriteLine("5. Increment AFE Device");
                SmartLog.WriteLine("6. Decrement AFE Device");
                SmartLog.WriteLine("7. Save AFE");
                SmartLog.WriteLine("\nPlease select the option from above:");
                var consoleKey = Console.ReadKey();
                if (consoleKey.KeyChar == '1') return 1;
                if (consoleKey.KeyChar == '2') return 2;
                if (consoleKey.KeyChar == '3') return 3;
                if (consoleKey.KeyChar == '4') return 4;
                if (consoleKey.KeyChar == '5') return 5;
                if (consoleKey.KeyChar == '6') return 6;
                if (consoleKey.KeyChar == '7') return 7;
            }
        }

        public List<Sensor> ReadDataPortConfig()
        {
            var collectCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("READ DATA PORT CONFIG", StringComparison.OrdinalIgnoreCase));
            if (collectCommand != null)
            {
                //collectCommand.MaxRetry = 15;
                ExecuteCommand(collectCommand);
            }

            return _sensors;
        }

        public List<Sensor> Collect()
        {
            var collectCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("COLLECT", StringComparison.OrdinalIgnoreCase));
            if (collectCommand != null)
            {
                //collectCommand.MaxRetry = 15;
                ExecuteCommand(collectCommand);
            }

            return _sensors;
        }

        public void PowerOff()
        {
            var powerOffCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("POWER OFF", StringComparison.OrdinalIgnoreCase));
            if (powerOffCommand != null)
                ExecuteCommand(powerOffCommand);
        }

        public string GetPort(string vId, string pId)
        {
            using (var searcher =
                new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portNames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList()
                    .Where(x => x["DeviceID"].ToString().Contains($"VID_{vId}+PID_{pId}"))
                    .Select(p => new
                    {
                        DeviceId = p["DeviceID"].ToString(),
                        Caption = p["Caption"].ToString()
                    });

                var port = portNames.FirstOrDefault(n => ports.Any(s => s.Caption.Contains(n)));
                return port;
            }
        }
    }
}