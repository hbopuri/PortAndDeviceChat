using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Smart.Agent.Helper;
using Smart.Agent.Model;

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
        private Sensor _accelerometerSensor;
        private Sensor _strainGageSensor;
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
            var hexString = _responseBuffer.ToHex();
            Console.WriteLine($"Response for {_currentCommand.CommandName}: {hexString}");
            _responseBuffer = null;

            //Console.WriteLine(ToHex(respBuffer));
            //Console.WriteLine(respBuffer.Length+":"+string.Join("|", respBuffer));
        }

        private void ProcessReadConfig(byte[] responseBuffer)
        {
            var position = 12;
            _dataPortConfig = new DataPortConfig
            {
                FirmwareVersion = responseBuffer.Skip(position).Take(2).Reverse().ToArray(),
                SampleInterval = responseBuffer.Skip(position+2).Take(2).Reverse().ToArray(),
                PreTriggerSamples = responseBuffer.Skip(position+4).Take(1).Reverse().ToArray(),
                ModeFlag = responseBuffer.Skip(position+5).Take(1).Reverse().ToArray(),
                ModePeriod = responseBuffer.Skip(position+6).Take(2).Reverse().ToArray(),
                ConfigTimestamp = responseBuffer.Skip(position+8).Take(4).Reverse().ToArray(),
                TriggerThreshold = responseBuffer.Skip(position+12).Take(2).Reverse().ToArray(),
                CommandModeTimeout = responseBuffer.Skip(position+14).Take(1).Reverse().ToArray(),
                ActChannelsDataPacking = responseBuffer.Skip(position+15).Take(1).Reverse().ToArray(),
                SleepModeBtOnTime = responseBuffer.Skip(position+16).Take(1).Reverse().ToArray(),
                SleepModeBtOffTime = responseBuffer.Skip(position+17).Take(1).Reverse().ToArray(),
                AmbientTemp = responseBuffer.Skip(position+18).Take(1).Reverse().ToArray(),
                MspTemp = responseBuffer.Skip(position+19).Take(1).Reverse().ToArray(),
                TriggerType = responseBuffer.Skip(position+20).Take(1).Reverse().ToArray(),
                PowerFault = responseBuffer.Skip(position+21).Take(1).Reverse().ToArray()
            };
            _dataPortConfig.SensorChannels.Add(new SensorChannel
            {
                Active = responseBuffer.Skip(position+22).Take(1).Reverse().ToArray(),
                Type = responseBuffer.Skip(position+23).Take(1).Reverse().ToArray(),
                Gain = responseBuffer.Skip(position+24).Take(4).Reverse().ToArray(),
                Offset = responseBuffer.Skip(position+28).Take(4).Reverse().ToArray(),
                SensitivityGageFactor = responseBuffer.Skip(position+32).Take(2).Reverse().ToArray(),
                AbsoluteR = responseBuffer.Skip(position+34).Take(2).Reverse().ToArray(),
                CalibrationTemp = responseBuffer.Skip(position+36).Take(1).Reverse().ToArray(),
                MonitoringTemp = responseBuffer.Skip(position+37).Take(1).Reverse().ToArray(),
                WriteCount = responseBuffer.Skip(position+38).Take(1).Reverse().ToArray(),
                SamplingFrequency = responseBuffer.Skip(position+39).Take(1).Reverse().ToArray(),
                Location = responseBuffer.Skip(position+40).Take(1).Reverse().ToArray(),
                Distance = responseBuffer.Skip(position+41).Take(2).Reverse().ToArray(),
                Diagnostic = responseBuffer.Skip(position+43).Take(2).ToArray(),
                WriteFlagSerialNumber = responseBuffer.Skip(position+45).Take(2).Reverse().ToArray()
            });
            position = position+2;
            for (var i = 0; i < 6; i++)
            {
                _dataPortConfig.SensorChannels.Add(new SensorChannel
                {
                    Active = responseBuffer.Skip(position).Take(1).Reverse().ToArray(),
                    Type = responseBuffer.Skip(position + 1).Take(1).Reverse().ToArray(),
                    Gain = responseBuffer.Skip(position + 2).Take(4).Reverse().ToArray(),
                    Offset = responseBuffer.Skip(position + 6).Take(4).Reverse().ToArray(),
                    SensitivityGageFactor = responseBuffer.Skip(position + 10).Take(2).Reverse().ToArray(),
                    AbsoluteR = responseBuffer.Skip(position + 12).Take(2).Reverse().ToArray(),
                    CalibrationTemp = responseBuffer.Skip(position + 14).Take(1).Reverse().ToArray(),
                    MonitoringTemp = responseBuffer.Skip(position + 15).Take(1).Reverse().ToArray(),
                    WriteCount = responseBuffer.Skip(position + 16).Take(1).Reverse().ToArray(),
                    SamplingFrequency = responseBuffer.Skip(position + 17).Take(1).Reverse().ToArray(),
                    Location = responseBuffer.Skip(position + 18).Take(1).Reverse().ToArray(),
                    Distance = responseBuffer.Skip(position + 19).Take(2).Reverse().ToArray(),
                    Diagnostic = responseBuffer.Skip(position + 21).Take(2).Reverse().ToArray(),
                    WriteFlagSerialNumber = responseBuffer.Skip(position + 23).Take(2).Reverse().ToArray()
                });
                position = position + 23;
            }

            //position == 173
            _dataPortConfig.Afe1ModelAndSn = responseBuffer.Skip(position).Take(4).Reverse().ToArray();
            _dataPortConfig.Afe2ModelAndSn = responseBuffer.Skip(position + 4).Take(4).Reverse().ToArray();
            _dataPortConfig.Afe3ModelAndSn = responseBuffer.Skip(position + 8).Take(4).Reverse().ToArray();
            _dataPortConfig.AfeWriteProtect = responseBuffer.Skip(position + 12).Take(4).Reverse().ToArray();
        }

        private void ProcessSensorData(byte[] responseBuffer)
        {
            _accelerometerSensor = new Sensor
            {
                Type = SensorType.Accelerometer,
                Data = new List<SensorData>()
            };
            _strainGageSensor = new Sensor
            {
                Type = SensorType.StrainGage,
                Data = new List<SensorData>()
            };
            int position = 22;
            while (position <= 262)
            {
                _accelerometerSensor.Data.Add(new SensorData{ Bytes = responseBuffer.Skip(position).Take(3).ToArray() });
                _strainGageSensor.Data.Add(new SensorData { Bytes = responseBuffer.Skip(position + 3).Take(3).ToArray() });
                position = position + 6;
            }

            foreach (var t in _accelerometerSensor.Data)
            {
                var x = t.Bytes;
                Array.Resize(ref x, 8);
                t.Value = BitConverter.ToInt64(x, 0);
                var channelIndex = 0;
                var offsetArray = _dataPortConfig.SensorChannels[channelIndex].Offset;
                Array.Resize(ref offsetArray, 4);
                var offset = System.BitConverter.ToSingle(offsetArray, 0);

                var gainArray = _dataPortConfig.SensorChannels[channelIndex].Gain;
                Array.Resize(ref gainArray, 4);
                var gain = System.BitConverter.ToSingle(offsetArray, 0);

                var gageArray = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor;
                Array.Resize(ref gageArray, 2);
                var gage = System.BitConverter.ToInt16(gageArray, 0);

                t.Value = (t.Value + offset) - 2047 * gain * (0.9039 / gage);
            }

            _accelerometerSensor.Average = _accelerometerSensor.Data.Average(x => x.Value);

            foreach (var t in _strainGageSensor.Data)
            {
                var x = t.Bytes;
                Array.Resize(ref x, 8);
                t.Value = BitConverter.ToInt64(x, 0);
                var channelIndex = 1;
                var offsetArray = _dataPortConfig.SensorChannels[channelIndex].Offset;
                Array.Resize(ref offsetArray, 4);
                var offset = System.BitConverter.ToSingle(offsetArray, 0);

                var gainArray = _dataPortConfig.SensorChannels[channelIndex].Gain;
                Array.Resize(ref gainArray, 4);
                var gain = System.BitConverter.ToSingle(offsetArray, 0);

                var gageArray = _dataPortConfig.SensorChannels[channelIndex].SensitivityGageFactor;
                Array.Resize(ref gageArray, 2);
                var gage = System.BitConverter.ToInt16(gageArray, 0);

                var absoluteArray = _dataPortConfig.SensorChannels[channelIndex].AbsoluteR;
                Array.Resize(ref absoluteArray, 2);
                var absoluteR = System.BitConverter.ToInt16(gageArray, 0);

                t.Value = (t.Value + offset) - 1092 * gain * ((0.00105026/ (absoluteR /100))/gage);
            }

            _strainGageSensor.Average = _strainGageSensor.Data.Average(x => x.Value);
            //var offset1 = System.BitConverter.ToSingle(_dataPortConfig.SensorChannels[1].Offset, 0);
            //var offset = _dataPortConfig.SensorChannels[1].Offset.ToType<float>();
            //Console.WriteLine("240 Bytes of Collect Response");
            //Console.WriteLine(string.Join("|", data));
        }
        private void port_DataReceived(object sender,
            SerialDataReceivedEventArgs e)
        {
            // Show all the incoming data in the port's buffer
            int bytes = _port.BytesToRead;
            //Console.WriteLine($"Trying to Read {bytes} bytes");
            byte[] buffer = new byte[bytes];
            _port.Read(buffer, 0, bytes);
            HandleSerialData(buffer);
            //var response = port.ReadExisting();
            //Console.WriteLine(response);
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
            Console.WriteLine($"Opened: {_port.PortName}");
            foreach (var queue in _commaQueue.CommandQueue.Where(x => x.SequenceId > 0).OrderBy(x => x.SequenceId))
            {
                ExecuteCommand(queue);
            }

            Console.WriteLine("--- Completed the Command Queue ---");
        }

        private void ExecuteCommand(Queue queue)
        {
            _currentCommand = queue;
            var command = queue.CommBytes;
            while (queue.MaxRetry >= 1)
            {
                Console.WriteLine($"***** Sending {queue.CommandName} Command *****");
                Console.WriteLine($"Command: {queue.CommBytes.ToHex()}");
                try
                {
                    _port.Write(command, 0, command.Length);
                    if (queue.WaitForNext > 0)
                    {
                        Console.WriteLine($"Sleeping for {queue.WaitForNext} seconds");
                        Thread.Sleep(TimeSpan.FromSeconds(queue.WaitForNext));
                    }

                    queue.MaxRetry = 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message);
                    if (queue.WaitForNext > 0)
                    {
                        Console.WriteLine($"Sleeping for {queue.RetryWait} seconds before retry");
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
                Console.WriteLine("Port cannot be Null or Empty");
                return;
            }

            using (var port = new SerialWrapper($@"\\.\{_comPortName.ToUpper()}", 9600, SerialWrapper.StopBits.One,
                SerialWrapper.Parity.None))
            {
                if (!port.Open())
                {
                    Console.WriteLine("Unable to connect.");
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

        public void Collect()
        {
            var collectCommand = _commaQueue.CommandQueue.FirstOrDefault(x =>
                x.CommandName.Equals("COLLECT", StringComparison.OrdinalIgnoreCase));
            if(collectCommand != null)
                ExecuteCommand(collectCommand);
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