using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Smart.Agent.Constant;
using Smart.Agent.Model;
using Smart.Hid;
using Smart.Log;
using Smart.Log.Enum;
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
        private static Options _options;
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

            //if (_currentCommand.CommandType == CommandType.Collect)
            //{
            //    Task.Run(() => SmartLog.WriteLine(_responseBuffer.ToHex()));
            //}

            if (_responseBuffer.Length != _currentCommand.ExpectedPacketSize)
                return;
            if (_currentCommand.CommandType == CommandType.ReadDataPort)
            {
                var readResponse = _responseBuffer;
                Task.Run(() => ProcessReadConfig(readResponse));
            }

            if (_currentCommand.CommandType == CommandType.Collect)
            {
                var collectResponse = _responseBuffer;
                Task.Run(() => ProcessSensorData(collectResponse));
            }

            byte checksum;
            if (_currentCommand.CommandType == CommandType.Collect)
            {
                var tempBuffer = _responseBuffer;
                var acknowledgementArray = tempBuffer.Take(14).ToArray();
                checksum = acknowledgementArray.Take(acknowledgementArray.Length - 1).ToArray().CheckSum();
                if (_options.PrintResponse)
                {
                    var hexString = acknowledgementArray.ToHex();
                    SmartLog.WriteLine(
                        $"\tResponse for {_currentCommand.CommandName} Acknowledgement (Checksum {new[] { checksum }.ToHex()} Length:{acknowledgementArray.Length}): {hexString}");
                }

                if (acknowledgementArray[acknowledgementArray.Length - 1] != checksum)
                {
                    SmartLog.WriteErrorLine(
                        $"*** Invalid Checksum For Collect Acknowledgement Response (CheckSum {new[] {checksum}.ToHex()}***");
                }

                var sensorDataArray = tempBuffer.Skip(14).Take(249).ToArray();
                checksum = sensorDataArray.Take(sensorDataArray.Length - 1).ToArray().CheckSum();
                if (_options.PrintResponse)
                {
                    var hexString = sensorDataArray.ToHex();
                    SmartLog.WriteLine(
                        $"\tResponse for {_currentCommand.CommandName} Sensor Data (Checksum {new[] { checksum }.ToHex()} Length:{sensorDataArray.Length}): {hexString}");
                }

                if (sensorDataArray[sensorDataArray.Length - 1] != checksum)
                {
                    SmartLog.WriteErrorLine(
                        $"*** Invalid Checksum For Collect Sensor Data Response (Checksum {new[] {checksum}.ToHex()}***");
                }

                var completeArray = tempBuffer.Skip(263).Take(14).ToArray();
                checksum = completeArray.Take(completeArray.Length - 1).ToArray().CheckSum();

                if (_options.PrintResponse)
                {
                    var hexString = completeArray.ToHex();
                    SmartLog.WriteLine(
                        $"\tResponse for {_currentCommand.CommandName} Success/Complete (Checksum {new[] { checksum }.ToHex()} Length:{completeArray.Length}): {hexString}");
                }

                if (completeArray[completeArray.Length - 1] != checksum)
                {
                    SmartLog.WriteErrorLine(
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

                if (_options.PrintResponse)
                {
                    var hexString = _responseBuffer.ToHex();
                    SmartLog.WriteLine(
                        $"\tResponse for {_currentCommand.CommandName} (CheckSum {new[] { checksum }.ToHex()} Length:{_responseBuffer.Length}): {hexString}");
                }
            }

            SmartLog.WriteLine($"\tResponse Buffer Length: {_responseBuffer.Length}");
        }

        private void ProcessReadConfig(byte[] responseBuffer)
        {
            var position = 12;
            _dataPortConfig = new DataPortConfig
            {
                FirmwareVersion = responseBuffer.Skip(position).Take(2).Reverse().ToArray(),
                SampleInterval = responseBuffer.Skip(position + 2).Take(2).Reverse().ToArray(), // 15-16
                PreTriggerSamples = responseBuffer.Skip(position + 4).Take(1).ToArray(),
                ModeFlag = responseBuffer.Skip(position + 5).Take(1).ToArray(),
                ModePeriod = responseBuffer.Skip(position + 6).Take(2).Reverse().ToArray(),
                ConfigTimestamp = responseBuffer.Skip(position + 8).Take(4).Reverse().ToArray(),
                ConfigTimestampUtc = responseBuffer.Skip(position + 8).Take(4).Reverse().ToArray().ToDateTime(),
                TriggerThreshold = responseBuffer.Skip(position + 12).Take(2).Reverse().ToArray(),
                CommandModeTimeout = responseBuffer.Skip(position + 14).Take(1).ToArray(),
                ActChannelsDataPacking = responseBuffer.Skip(position + 15).Take(1).ToArray(), //31
                SleepModeBtOnTime = responseBuffer.Skip(position + 16).Take(1).ToArray(),
                SleepModeBtOffTime = responseBuffer.Skip(position + 17).Take(1).ToArray(),
                AmbientTemp = responseBuffer.Skip(position + 18).Take(1).ToArray(),
                MspTemp = responseBuffer.Skip(position + 19).Take(1).ToArray(),
                TriggerType = responseBuffer.Skip(position + 20).Take(1).ToArray(),
                PowerFault = responseBuffer.Skip(position + 21).Take(1).ToArray(),
                CompleteResponseBuffer = responseBuffer
            };
            position = position + 22;

            int channel = 0;

            if (Convert.ToInt32(_dataPortConfig.ActChannelsDataPacking.ToHex()) ==  (int)ChannelMode.TwoChannel)
            {
                channel = 2;
            }
            else if (Convert.ToInt32(_dataPortConfig.ActChannelsDataPacking.ToHex()) == (int)ChannelMode.FourChannel)
            {
                channel = 4;
            }
            else if (Convert.ToInt32(_dataPortConfig.ActChannelsDataPacking.ToHex()) == (int)ChannelMode.SixChannel)
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
            if(!_options.PrintDataPortConfig)
                return;
            SmartLog.WriteLine("-- General and Diagnostics Only ---");
            SmartLog.WriteLine($"\tFirmware Version: {_dataPortConfig.FirmwareVersion.ToDecimal(0)}");
            SmartLog.WriteLine($"\tSample Interval: {_dataPortConfig.SampleInterval.ToDecimal(0)}");
            SmartLog.WriteLine($"\tPreTrigger Samples: {_dataPortConfig.PreTriggerSamples.ToDecimal(0)}");
            SmartLog.WriteLine($"\tMode Flag: {_dataPortConfig.ModeFlag.ToHex()}");
            SmartLog.WriteLine($"\tMode Period: {_dataPortConfig.ModePeriod.ToDecimal(0)}");
            SmartLog.WriteLine($"\tConfig Timestamp: {_dataPortConfig.ConfigTimestamp.ToDecimal(0)}");
            SmartLog.WriteLine($"\tConfig Timestamp (UTC): {_dataPortConfig.ConfigTimestamp.ToDateTime()}");
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
                            Data = new List<SensorData>(),
                            Type = SensorType.Accelerometer
                        });
                        break;
                    case (int) SensorType.StrainGauge:
                        _sensors.Add(new Sensor
                        {
                            Afe = new List<int> {0, 1}.Any(x => _dataPortConfig.SensorChannels.IndexOf(channel) == x)
                                ? Afe.Top
                                : new List<int> {2, 3}.Any(x => _dataPortConfig.SensorChannels.IndexOf(channel) == x)
                                    ? Afe.Tip
                                    : Afe.Mid,
                            Data = new List<SensorData>(),
                            Type = SensorType.StrainGauge
                        });
                        break;
                }
            }

            int position = 22;
            var isCompressed = _dataPortConfig.ActChannelsDataPacking.ToBitArray()[0];
            var channelMode = Convert.ToInt32(_dataPortConfig.ActChannelsDataPacking.ToHex(true));
            if (isCompressed)
            {
                while (position < 262)
                {
                    var currentThree = responseBuffer.Skip(position).Take(3).ToArray();
                    MakeTwoHalves(currentThree, out var firstHalfBytes, out var secondHalfBytes);

                    if (_sensors[0].Type == SensorType.Accelerometer)
                    {
                        if (_sensors.Any(x => x.Afe == Afe.Top && x.Type == SensorType.Accelerometer))
                        {
                           
                                _sensors.First(x => x.Afe == Afe.Top && x.Type == SensorType.Accelerometer).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = firstHalfBytes
                                        });
                        }
                    }
                    else if (_sensors[0].Type == SensorType.StrainGauge)
                    {
                        if (_sensors.Any(x => x.Afe == Afe.Top && x.Type == SensorType.StrainGauge))
                        {
                            _sensors.First(x => x.Afe == Afe.Top && x.Type == SensorType.StrainGauge).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = firstHalfBytes
                                    });
                        }
                    }

                    if (_sensors[1].Type == SensorType.Accelerometer)
                    {
                        if (_sensors.Any(x => x.Afe == Afe.Top && x.Type == SensorType.Accelerometer))
                        {
                            _sensors.First(x => x.Afe == Afe.Top && x.Type == SensorType.Accelerometer).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = secondHalfBytes
                                    });
                        }
                    }
                    else if (_sensors[1].Type == SensorType.StrainGauge)
                    {
                        if (_sensors.Any(x => x.Afe == Afe.Top && x.Type == SensorType.StrainGauge))
                        {
                            _sensors.First(x => x.Afe == Afe.Top && x.Type == SensorType.StrainGauge).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = secondHalfBytes
                                    });
                        }
                    }



                    position = position + 3;
                    if (channelMode == (int) ChannelMode.FourChannel || channelMode == (int) ChannelMode.SixChannel)
                    {
                        currentThree = responseBuffer.Skip(position + 3).Take(3).ToArray();
                        MakeTwoHalves(currentThree, out firstHalfBytes, out secondHalfBytes);
                        if (_sensors[2].Type == SensorType.Accelerometer)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Tip && x.Type == SensorType.Accelerometer))
                            {
                                _sensors.First(x => x.Afe == Afe.Tip && x.Type == SensorType.Accelerometer).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = firstHalfBytes
                                        });
                            }
                        }
                        else if (_sensors[2].Type == SensorType.StrainGauge)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Tip && x.Type == SensorType.StrainGauge))
                            {
                                _sensors.First(x => x.Afe == Afe.Tip && x.Type == SensorType.StrainGauge).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = firstHalfBytes
                                        });
                            }
                        }

                        if (_sensors[3].Type == SensorType.Accelerometer)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Tip && x.Type == SensorType.Accelerometer))
                            {
                                _sensors.First(x => x.Afe == Afe.Tip && x.Type == SensorType.Accelerometer).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = secondHalfBytes
                                        });
                            }
                        }
                        else if (_sensors[3].Type == SensorType.StrainGauge)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Tip && x.Type == SensorType.StrainGauge))
                            {
                                _sensors.First(x => x.Afe == Afe.Tip && x.Type == SensorType.StrainGauge).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = secondHalfBytes
                                        });
                            }
                        }

                        position = position + 3;
                    }

                    if (channelMode == (int) ChannelMode.SixChannel)
                    {
                        currentThree = responseBuffer.Skip(position + 3).Take(3).ToArray();
                        MakeTwoHalves(currentThree, out firstHalfBytes, out secondHalfBytes);
                        if (_sensors[4].Type == SensorType.Accelerometer)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Mid && x.Type == SensorType.Accelerometer))
                            {
                                _sensors.First(x => x.Afe == Afe.Mid && x.Type == SensorType.Accelerometer).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = firstHalfBytes
                                        });
                            }
                        }
                        else if (_sensors[4].Type == SensorType.StrainGauge)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Mid && x.Type == SensorType.StrainGauge))
                            {
                                _sensors.First(x => x.Afe == Afe.Mid && x.Type == SensorType.StrainGauge).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = firstHalfBytes
                                        });
                            }
                        }

                        if (_sensors[5].Type == SensorType.Accelerometer)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Mid && x.Type == SensorType.Accelerometer))
                            {
                                _sensors.First(x => x.Afe == Afe.Mid && x.Type == SensorType.Accelerometer).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = secondHalfBytes
                                        });
                            }
                        }
                        else if (_sensors[5].Type == SensorType.StrainGauge)
                        {
                            if (_sensors.Any(x => x.Afe == Afe.Mid && x.Type == SensorType.StrainGauge))
                            {
                                _sensors.First(x => x.Afe == Afe.Mid && x.Type == SensorType.StrainGauge).Data
                                    .Add(
                                        new SensorData
                                        {
                                            Bytes = secondHalfBytes
                                        });
                            }
                        }

                        position = position + 3;
                    }
                }
            }
            else // uncompressed data
            {
                while (position < 262)
                {

                    //var currentFour = responseBuffer.Skip(position).Take(4).ToArray();

                    if (_sensors.Any(x => x.Afe == Afe.Top && x.Type == SensorType.Accelerometer))
                    {
                        _sensors.First(x => x.Afe == Afe.Top).Data
                            .Add(
                                new SensorData
                                {
                                    Bytes = responseBuffer.Skip(position).Take(2).ToArray()
                                });
                    }

                    if (_sensors.Any(x => x.Afe == Afe.Top && x.Type == SensorType.StrainGauge))
                    {
                        _sensors.First(x => x.Afe == Afe.Top).Data
                            .Add(
                                new SensorData
                                {
                                    Bytes = responseBuffer.Skip(position + 2).Take(2).ToArray()
                                });
                    }

                    position = position + 4;
                    if (channelMode == (int) ChannelMode.FourChannel || channelMode == (int) ChannelMode.SixChannel)
                    {
                        //currentFour = responseBuffer.Skip(position + 4).Take(4).ToArray();
                        if (_sensors.Any(x => x.Afe == Afe.Tip && x.Type == SensorType.Accelerometer))
                        {
                            _sensors.First(x => x.Afe == Afe.Tip).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = responseBuffer.Skip(position + 4).Take(2).ToArray()
                                    });
                        }

                        if (_sensors.Any(x => x.Afe == Afe.Tip && x.Type == SensorType.StrainGauge))
                        {
                            _sensors.First(x => x.Afe == Afe.Tip).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = responseBuffer.Skip(position + 6).Take(2).ToArray()
                                    });
                        }

                        position = position + 4;
                    }

                    if (channelMode == (int) ChannelMode.SixChannel)
                    {
                        //currentFour = responseBuffer.Skip(position + 4).Take(4).ToArray();
                        if (_sensors.Any(x => x.Afe == Afe.Mid && x.Type == SensorType.Accelerometer))
                        {
                            _sensors.First(x => x.Afe == Afe.Mid).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = responseBuffer.Skip(position + 4).Take(2).ToArray()
                                    });
                        }

                        if (_sensors.Any(x => x.Afe == Afe.Mid && x.Type == SensorType.StrainGauge))
                        {
                            _sensors.First(x => x.Afe == Afe.Mid).Data
                                .Add(
                                    new SensorData
                                    {
                                        Bytes = responseBuffer.Skip(position + 6).Take(2).ToArray()
                                    });
                        }

                        position = position + 4;
                    }
                }
            }

            int channelIndex = 0;
            foreach (var sensor in _sensors)
            {
                //SmartLog.WriteLine($"{sensor.Afe}-{sensor.Type}:");
                //foreach (var sensorData in sensor.Data)
                //{
                //    if(sensorData.Bytes != null)
                //        SmartLog.WriteLine(sensorData.Bytes.ToDecimal(0).ToString(CultureInfo.InvariantCulture));
                //}
                switch (sensor.Afe)
                {
                    case Afe.Top:
                        if (sensor.Type == SensorType.Accelerometer && sensor.Data.Any(x => x.Bytes != null))
                            CalculateAccelerometer(sensor, channelIndex);
                        if (sensor.Type == SensorType.StrainGauge && sensor.Data.Any(x => x.Bytes != null))
                            CalculateStrain(sensor, channelIndex);
                        break;
                    case Afe.Tip:
                        if (sensor.Type == SensorType.Accelerometer && sensor.Data.Any(x => x.Bytes != null))
                            CalculateAccelerometer(sensor, channelIndex);

                        if (sensor.Type == SensorType.StrainGauge && sensor.Data.Any(x => x.Bytes != null))
                            CalculateStrain(sensor, channelIndex);
                        break;
                    case Afe.Mid:
                        if (sensor.Type == SensorType.Accelerometer && sensor.Data.Any(x => x.Bytes != null))
                            CalculateAccelerometer(sensor, channelIndex);

                        if (sensor.Type == SensorType.StrainGauge && sensor.Data.Any(x => x.Bytes != null))
                            CalculateStrain(sensor, channelIndex);

                        break;
                }

                channelIndex++;

            }
        }

        private void CalculateStrain(Sensor sensor, int channelIndex)
        {
            foreach (var t in sensor.Data.Where(x => x.Bytes != null))
            {
                var offset = _dataPortConfig.SensorChannels[channelIndex].Offset.ToDecimal(0);
                var gain = _dataPortConfig.SensorChannels[channelIndex].Gain.ToFloat(0);
                var gage = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor.ToDecimal(0);
                var absoluteR = _dataPortConfig.SensorChannels[channelIndex].AbsoluteR.ToDecimal(0);
                var n = t.Bytes.ToArray();
                var someA = (BitConverter.ToUInt16(n, 0) + (double) offset - 1092) * gain *
                            (double) (((decimal) 0.00105026 / (absoluteR / 100)) / (gage / 1000));
                if (_dataPortConfig.SensorChannels[channelIndex].Type.ToDecimal(0) == 1)
                {
                    someA = someA * 1000000;
                }

                t.Value = someA;
            }
        }

        private void CalculateAccelerometer(Sensor sensor, int channelIndex)
        {
            foreach (var t in sensor.Data.Where(x => x.Bytes != null))
            {
                var offset = _dataPortConfig.SensorChannels[channelIndex].Offset.ToDecimal(0);
                var gain = _dataPortConfig.SensorChannels[channelIndex].Gain.ToFloat(0);
                var gage = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor.ToDecimal(0);
                var n = t.Bytes.ToArray();
                var someA = (((BitConverter.ToUInt16(n, 0) + (double) offset) - 2047) * gain) *
                            (0.9039 / (double) (gage / 1000));

                if (_dataPortConfig.SensorChannels[channelIndex].Type.ToDecimal(0) == 1)
                {
                    someA = someA * 1000000;
                }

                t.Value = someA;
            }
        }

        //private static void GetAccAndStrainInDouble(byte[] currentThree, out double accelerometer, out double strain)
        //{
        //    var first = currentThree.ToBinary().Split('-')[0];
        //    //var firstLeft = first.Substring(4, 4)+ "0000";

        //    var second = currentThree.ToBinary().Split('-')[1];
        //    var secondLeft = second.Substring(4, 4) + "0000";

        //    var secondRightFromLeft = secondLeft.Substring(0, 4);
        //    secondRightFromLeft = "0000" + secondRightFromLeft;


        //    var high = Convert.ToInt32(secondRightFromLeft, 2);
        //    var low = Convert.ToInt32(first, 2);
        //    accelerometer = high * Math.Pow(2, 8) + low;

        //    var secondRight = "0000" + second.Substring(0, 4);
        //    var third = currentThree.ToBinary().Split('-')[2];
        //    var thirdLeft = third.Substring(4, 4) + "0000";
        //    var thirdRight = "0000" + third.Substring(0, 4);

        //    low = Convert.ToInt32(secondRight, 2) + Convert.ToInt32(thirdLeft, 2);
        //    high = Convert.ToInt32(thirdRight, 2);
        //    strain = high * Math.Pow(2, 8) + low;
        //}

        private static void MakeTwoHalves(byte[] currentThree, out byte[] accelerometer, out byte[] strain)
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

        public async Task Init(byte interfaceId, string commPort, Options options)
        {
            _options = options;
            _port = new SerialPort(commPort,
                9600, Parity.None, 8, StopBits.One);
            _port.DataReceived += port_DataReceived;
            _commaQueue = new Queue(interfaceId);
            _commaQueue.LoadLcmQueue();
            await UsbToSpiConverter.Init();
        }

        public bool Start()
        {
            try
            {
                if (!_port.IsOpen)
                    _port.Open();
                SmartLog.WriteLine($"Opened: {_port.PortName}");
                foreach (var queue in _commaQueue.CommandQueue.Where(x => x.SequenceId > 0).OrderBy(x => x.SequenceId))
                {
                    ExecuteCommand(queue);
                }

                return true;
            }
            catch (Exception e)
            {
                SmartLog.WriteLine(e.Message);
                return false;
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
                if(_options.PrintRequest)
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
                case CommandType.Collect:
                    try
                    {
                        _sensors = new List<Sensor>();
                        loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.ReadDataPort:
                    try
                    {
                        loopResponse.ReturnObject = ReadDataPortConfig();
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
                case CommandType.StartEdcAllOver:
                    loopResponse.ReturnObject = Start();
                    return await Task.FromResult(loopResponse);
                case CommandType.IncrementSg:
                    try
                    {
                        await UsbToSpiConverter.IncrementOrDecrementStrain(SgAdjust.Increment);
                        //loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.DecrementSg:
                    try
                    {
                        await UsbToSpiConverter.IncrementOrDecrementStrain(SgAdjust.Decrement);
                        // loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.MinAx:
                    try
                    {
                        await UsbToSpiConverter.IncrementOrDecrementAx(AxAdjust.Min);
                        //loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.MidAx:
                    try
                    {
                        await UsbToSpiConverter.IncrementOrDecrementAx(AxAdjust.Mid);
                        //loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.MaxAx:
                    try
                    {
                        await UsbToSpiConverter.IncrementOrDecrementAx(AxAdjust.Max);
                        //loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.SaveSg:
                    try
                    {
                        await UsbToSpiConverter.IncrementOrDecrementStrain(SgAdjust.Save);
                        //loopResponse.ReturnObject = Collect();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.PowerOff:
                    try
                    {
                        PowerOff();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        SmartLog.WriteLine(ex.ToString());
                    }

                    return await Task.FromResult(loopResponse);
                case CommandType.WriteDataPort:
                    try
                    {
                        var response = ReadDataPortConfig();
                        WriteDataPortConfig(_currentCommand.CommBytes[0], response, GetDefaultDataPortConfig());
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

        public int Menu(bool printMenu)
        {
            while (true)
            {
                if (printMenu)
                {
                    //Console.Clear();
                    SmartLog.WriteLine("\nSerial Port Menu");
                    SmartLog.WriteLine("--------------------------------------------------------");
                    //SmartLog.WriteLine();
                    SmartLog.WriteLine("A. Issue COLLECT Command");
                    SmartLog.WriteLine("B. Issue READ DATA PORT CONFIG Command");
                    //SmartLog.WriteLine("3. Move Next");
                    SmartLog.WriteLine("C. Start EDC All Over");
                    SmartLog.WriteLine("D. Increment Strain Gauge");
                    SmartLog.WriteLine("E. Decrement Strain Gauge");
                    SmartLog.WriteLine("F. Min Accelerometer");
                    SmartLog.WriteLine("G. Mid Accelerometer");
                    SmartLog.WriteLine("H. Max Accelerometer");
                    SmartLog.WriteLine("I. Save AFE");
                    SmartLog.WriteLine("J. Power Off");
                    SmartLog.WriteLine("K. Force Write Date Port Config");
                    SmartLog.WriteLine("\nPlease select the option from above:");
                }

                var consoleKey = Console.ReadKey();
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'A') return 1;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'B') return 2;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'C') return 4;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'D') return 5;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'E') return 6;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'F') return 7;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'G') return 8;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'H') return 9;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'I') return 10;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'J') return 11;
                if (char.ToUpperInvariant(consoleKey.KeyChar) == 'K') return 12;
            }
        }

        public DataPortConfig ReadDataPortConfig()
        {
            var collectCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("READ DATA PORT CONFIG", StringComparison.OrdinalIgnoreCase));
            if (collectCommand != null)
            {
                //collectCommand.MaxRetry = 15;
                ExecuteCommand(collectCommand);
            }

            return _dataPortConfig;
        }

        public List<Sensor> Collect()
        {
            var collectCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("COLLECT", StringComparison.OrdinalIgnoreCase));
            if (collectCommand != null)
            {
                collectCommand.WaitForNext = 3;
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

        public DataPortConfig GetDefaultDataPortConfig()
        {
            return new DataPortConfig
            {
                FirmwareVersion = new byte[] {0x02, 0xBC},
                ActChannelsDataPacking = new byte[] {0x41},
                //ActChannelsDataPacking = new byte[] { 0x21 },
                SampleInterval = new byte[] {0x00, 0x04},
                ModeFlag = new byte[] {0x41} // Turn On Mode Flag
                //ModeFlag = new byte[] { 0x42 } // Turn Off Mode Flag
            };
        }

        public void WriteDataPortConfig(byte boardId, DataPortConfig received, DataPortConfig defaultSettings)
        {
            var complete = received.CompleteResponseBuffer;

            complete[0] = boardId;
            complete[1] = 0xC4;
            complete[2] = 0x09;

            complete[6] = 0xBB;
            complete[7] = 0x00;
            complete[8] = 0x00;
            complete[9] = 0x11;


            //complete[12] = defaultSettings.FirmwareVersion[1];
            //complete[13] = defaultSettings.FirmwareVersion[0];

            //complete[12] = 0x76;
            //complete[13] = 0x02;

            complete[14] = defaultSettings.SampleInterval[1];
            complete[15] = defaultSettings.SampleInterval[0];
            //complete[14] = 0x02;
            //complete[15] = 0x00;

            complete[17] = defaultSettings.ModeFlag[0];

            var currentUtcTime = DateTime.UtcNow.ToFourBytes();
            complete[20] = currentUtcTime[3];
            complete[21] = currentUtcTime[2];
            complete[22] = currentUtcTime[1];
            complete[23] = currentUtcTime[0];

            //SmartLog.WriteErrorLine($"currentUtcTime: {currentUtcTime.ToDecimal(0)}");

            complete[27] = defaultSettings.ActChannelsDataPacking[0];
            //complete[27] = 0x21;

            complete[complete.Length - 1] = complete.Take(complete.Length - 1).ToArray().CheckSum();

            var writeDataPortCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("WRITE DATA PORT CONFIG", StringComparison.OrdinalIgnoreCase));
            if (writeDataPortCommand != null)
            {
                writeDataPortCommand.CommBytes = complete;
                ExecuteCommand(writeDataPortCommand);
            }

            ReadDataPortConfig();
        }
    }
}