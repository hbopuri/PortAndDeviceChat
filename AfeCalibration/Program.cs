using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json;
using Smart.Agent.Business;
using Smart.Agent.Constant;
using Smart.Agent.Helper;
using Smart.Agent.Model;
using Smart.Log;
using Smart.Log.Helper;

namespace AfeCalibration
{
    class Program
    {
        private static byte _boardId;
        private static SmartPort _smartPort;
        private static string _defaultComPort;
        private static Options _options;
        private static bool _printMenu = true;
        private static bool _isTip;
        private static int _accelIncDecInterval = 6;
        private static int _strainIncDecInterval = 3;
        private static DataPortConfig _readDataPortConfig;
        private static string filePath = $"{AppDomain.CurrentDomain.BaseDirectory}Balancing.json";
        private static DataPortConfig _defaultDataPortConfig;

        [STAThread]
        // ReSharper disable once UnusedParameter.Local
        static async Task Main(string[] args)
        {
            if (args == null)
                SmartLog.WriteToEvent($"AFE Cal - Nor Arguments: {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss")}");
            else
                SmartLog.WriteToEvent($"AFE Cal -{string.Join(", ", args)} :{DateTime.Now.ToString("MM / dd / yyyy hh: mm:ss")}");
            //return;
            await Run(args);
        }
        public static async Task Run(string[] args)
        {
            //Console.Clear();
            try
            {
                Parser.Default.ParseArguments<Options>(args)
                              .WithParsed(o =>
                              {
                                  _options = o;
                                  SmartLog.WriteLine(!string.IsNullOrWhiteSpace(o.BoardId)
                                      ? $"Entered IFB Board Id (in Hex). Current Arguments: --ifb {o.BoardId.ToUpper()}"
                                      : $"No IFB Board Id passed. Current Arguments: --ifb");
                                  SmartLog.WriteLine(o.PrintRequest
                                      ? $"Print Request enabled. Current Arguments: --req {o.PrintRequest}"
                                      : $"Print Request disabled. Current Arguments: --req {o.PrintRequest}");
                                  SmartLog.WriteLine(o.PrintResponse
                                      ? $"Print Response enabled. Current Arguments: --res {o.PrintResponse}"
                                      : $"Print Response disabled. Current Arguments: --res {o.PrintResponse}");
                                  SmartLog.WriteLine(o.PrintDataPortConfig
                                      ? $"Print Data Port Config enabled. Current Arguments: --conf {o.PrintDataPortConfig}"
                                      : $"Print Data Port Config disabled. Current Arguments: --conf {o.PrintDataPortConfig}");
                                  SmartLog.WriteLine(o.Model == 1 || o.Model == 2
                                      ? $"Entered Model. Current Arguments: --mdl {o.Model + (o.Model == 1 ? ":Ax-Sg" : ":Sg-Sg")}"
                                      : $"No Model entered. Current Arguments: --mdl");
                                  SmartLog.WriteLine(o.Position == 1 || o.Position == 2
                                    ? $"Entered Top/Tip. Current Arguments: --pos {o.Position + (o.Position == 1 ? ":Top" : ":Tip")}"
                                    : $"No Top/Tip entered. Current Arguments: --pos");
                                  SmartLog.WriteLine(!string.IsNullOrWhiteSpace(o.Command)
                                     ? $"Entered Command. Current Arguments: --cmd {o.Command.ToUpper()}"
                                     : $"No Command passed. Current Arguments: --cmd");
                                  SmartLog.WriteLine(
                                    $"MemsTestCycle={o.MemsTestCycle}. Current Arguments: --memsCycle");
                                  SmartLog.WriteLine(
                                   $"MemsTestMin={o.MemsTestMin}. Current Arguments: --memsMin");
                                  SmartLog.WriteLine(
                                  $"MemsTestMax={o.MemsTestMax}. Current Arguments: --memsMax");
                              });
            }
            catch (Exception exception)
            {
                //SmartLog.WriteToEvent(exception.Message);
                SmartLog.WriteErrorLine(exception.ToString());
            }
            finally
            {
                if (_options == null || string.IsNullOrWhiteSpace(_options.BoardId))
                {
                    _options = new Options
                    {
                        BoardId = args[0],
                        Model = Convert.ToInt32(args[1]),
                        Position = Convert.ToInt32(args[2])
                    };
                    if(_options.Model == 1 &&  args[3] != null)
                    {
                        try
                        {
                            var mems = args[3].Split('-');
                            _options.MemsTestCycle = Convert.ToInt32(mems[0]);
                            _options.MemsTestMin = Convert.ToInt32(mems[1]);
                            _options.MemsTestMax = Convert.ToInt32(mems[2]);
                        }
                        catch (Exception exception)
                        {
                            SmartLog.WriteErrorLine($"{exception.Message}");
                        }
                    }
                }
            }
          
            
            if (args != null && args.Any() && args[0].Equals("--help", StringComparison.InvariantCultureIgnoreCase))
                return;
            if (args != null && args.Any() && args[0].Equals("--version", StringComparison.InvariantCultureIgnoreCase))
                return;
          
            Greet();
            Init();
            if (_options == null)
                SmartLog.WriteToEvent($"_options  == null");
            else
                SmartLog.WriteToEvent($"BoardId: {_options.BoardId}, Model: {_options.Model}");
            if (!DetectComPort())
                return;
            GetBoardId();
            Console.WriteLine();
            _defaultDataPortConfig = _smartPort.GetDefaultDataPortConfig();
            await _smartPort.Init(_boardId, _defaultComPort, _options);
            if (!_smartPort.Start())
                return;
            try
            {
                if (!string.IsNullOrWhiteSpace(_options.Command))
                {
                    var command = (int)typeof(CommandType).GetField(_options.Command).GetValue(null);
                    if (command == CommandType.JustCollect)
                    {
                        _smartPort.Go(menuOption: CommandType.PowerOff).Wait();
                        _smartPort.Go(menuOption: CommandType.PowerOn).Wait();
                        _smartPort.Go(menuOption: CommandType.Connect).Wait();
                        var dataPortConfig = _smartPort.Go(menuOption: CommandType.ReadDataPort).Result;
                        var readings = _smartPort.Go(menuOption: CommandType.JustCollect).Result;
                        foreach (var sensor in ((List<Sensor>)readings.ReturnObject).Where(x => _isTip ? x.Afe == Afe.Tip : x.Afe == Afe.Top))
                        {
                            var quantized = Math.Truncate(sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x));
                            SmartLog.WriteHighlight(
                                $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{Math.Truncate(sensor.Data.Average(x => x.Value))}," +
                                $" Quantized: {quantized}");
                        }
                    }
                    else if (command == CommandType.MemsTest)
                    {
                        _smartPort.Go(menuOption: CommandType.PowerOff).Wait();
                        _smartPort.Go(menuOption: CommandType.PowerOn).Wait();
                        _smartPort.Go(menuOption: CommandType.Connect).Wait();
                        SmartLog.WriteHighlight($"Make sure Power Off, Power ON, IF and Connect are done before this");
                        _readDataPortConfig = _smartPort.ReadDataPortConfig();
                        MemsTest();
                    }
                    else
                        _smartPort.Go(menuOption: command).Wait();
                    return;
                }
                _readDataPortConfig = _smartPort.ReadDataPortConfig();
                _isTip = _options.Position == 2;
                // && _readDataPortConfig.SensorChannels[2].Active.ToDecimal(0) == 01
                //    && _readDataPortConfig.SensorChannels[3].Active.ToDecimal(0) == 01
                //if (_readDataPortConfig.SensorChannels.Count >2 )
                //{
                //    _isTip = true;
                //}
                //CompareDataPortConfig(readDataPortConfig, defaultDataPortConfig);
                var portTask = _smartPort.Go(menuOption: CommandType.Collect);
                while (portTask.Result.Selection != CommandType.Exit)
                {
                    switch (portTask.Result.Selection)
                    {
                        case CommandType.JustCollect:
                            {
                                if (portTask.Result.ReturnObject != null)
                                {
                                    var sensors = ((List<Sensor>)portTask.Result.ReturnObject);
                                    TipOrTop(sensors);
                                    PrintCollectResponse(sensors, SensorType.Both);
                                }
                                break;
                            }
                        case CommandType.Collect:
                            {
                                if (portTask.Result.ReturnObject != null)
                                {
                                    var sensors = ((List<Sensor>)portTask.Result.ReturnObject);
                                    TipOrTop(sensors);
                                    int maxRetry = 0;
                                    while (!sensors.Any() && maxRetry < 10)
                                    {
                                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                                        sensors = ((List<Sensor>)portTask.Result.ReturnObject);
                                        Thread.Sleep(TimeSpan.FromSeconds(3));
                                        maxRetry++;
                                    }
                                    if (maxRetry == 10)
                                    {
                                        SmartLog.WriteErrorLine("Faield to Receive Collect Resposne");
                                        break;
                                    }
                                    //if (sensors[1].Data != null && sensors[1].Data.Any())
                                    //{
                                    //    var channelThreeQuantized =
                                    //    //Math.Truncate(sensors[1].Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0))
                                    //    //    .Average(x => x));
                                    //    //Math.Truncate(sensors[1].Data.Average(x => x.Value));
                                    //    Math.Truncate(sensors[1].Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x));
                                    //    _isTip = channelThreeQuantized < 300 || channelThreeQuantized > 3800;
                                    //}

                                    PrintCollectResponse(sensors, SensorType.Both);
                                    //if (_options.Model.Equals(2))
                                    //    sensors = sensors.Skip(2).Take(2).ToList();

                                    //var values = sensors.Where(x => _isTip ? x.Afe == Afe.Tip : x.Afe == Afe.Top)
                                    //    .Select(x => x.Data.Select(d => BitConverter.ToUInt16(d.Bytes, 0)).Average(a => a)).Select(x => Math.Truncate(x)).ToList();


                                    await AdjustStrain(_readDataPortConfig, sensors);
                                    if(_options.Model == 1)
                                        await AdjustAx(_readDataPortConfig, sensors);
                                }

                                break;
                            }
                        case CommandType.ReadDataPort:
                            {
                                if (portTask.Result.ReturnObject != null)
                                {
                                    var dataPortConfig = ((DataPortConfig)portTask.Result.ReturnObject);
                                    CompareDataPortConfig(dataPortConfig, _defaultDataPortConfig);
                                }

                                break;
                            }
                    }

                    //portTask = _smartPort.Go(menuOption: _smartPort.Menu());
                    //portTask = _smartPort.Go(menuOption: _smartPort.Menu(_printMenu));
                    Console.WriteLine("Completed Job");
                    _printMenu = false;
                    break;
                }
            }
            catch (Exception exception)
            {
                SmartLog.WriteErrorLine(exception.Message);
                File.WriteAllText(filePath, exception.ToString());
                _ = _smartPort.Go(menuOption: CommandType.PowerOff);
            }

            //SmartLog.WriteLine("Type exit to quite\n");
            //while (!Convert.ToString(Console.ReadLine()).Equals("exit", StringComparison.OrdinalIgnoreCase))
            //{
            //    SmartLog.WriteLine("Unknown command\n");
            //}

            //_smartPort.PowerOff();
        }

        private static void TipOrTop(List<Sensor> sensors)
        {
            //if (!sensors.Any())
            //{
            //    return;
            //}
            //if(sensors.Count > 2)
            //{
            //    var sensorThree = Math.Truncate(sensors[3].Data.Average(x => x.Value));
            //    if(sensorThree >= 300 && sensorThree <= 3800)
            //    {
            //        _isTip = true;
            //    }
            //}
            //else if(sensors.Count == 2)
            //{
            //    var sensorOne = Math.Truncate(sensors[1].Data.Average(x => x.Value));
            //    if (sensorOne >= 300 && sensorOne <= 3800)
            //    {
            //        _isTip = false;
            //    }
            //}
        }

        private static async Task AdjustAx(DataPortConfig readDataPortConfig, List<Sensor> sensors)
        {
            if (readDataPortConfig.SensorChannels.Any(x =>
                x.Type.ToDecimal(0) == (int)SensorType.Accelerometer))
            {
                var axSensors = sensors.Where(x => x.Afe == (_isTip ? Afe.Tip : Afe.Top)
                                                   && x.Type == SensorType.Accelerometer).ToList();
                var isBalanced = false;
                var saveRequire = false;
                while (!isBalanced)
                {
                    var accelValue = axSensors.Select(s =>
                           Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0)))
                               .Average(b => b))).FirstOrDefault();
                    //var accelValue = axSensors.Select(s =>
                    //       Math.Truncate(s.Data.Average(x=>x.Value))).FirstOrDefault();

                    Task<LoopResponse> portTask;
                    while (axSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                        .All(x => x < AxRange.Min))
                    //if (accelValue < AxRange.Min)
                    {
                        saveRequire = true;


                        //var loopCount = (AxRange.Min - Convert.ToInt32(accelValue)) / _accelIncDecInterval;
                        //var reminder = accelValue == 0 
                        //    ? 0 
                        //    : AxRange.Min % Convert.ToInt32(accelValue);
                        //if (reminder > 0)
                        //    loopCount++;
                        //bool singleIncrement = false;
                        //if (Math.Abs(AxRange.Min - Convert.ToInt32(accelValue)) <= _accelIncDecInterval
                        //    || Math.Abs(AxRange.Max - Convert.ToInt32(accelValue)) <= _accelIncDecInterval)
                        //    singleIncrement = true;

                        //if (loopCount > 0 && !singleIncrement)
                        //{
                        //    while (loopCount > 0)
                        //    {
                        //        _smartPort.Go(menuOption: CommandType.MinAx).Wait();
                        //        loopCount--;
                        //    }
                        //}
                        //else
                        {
                            await _smartPort.Go(menuOption: CommandType.MinAx);
                        }
                        //if(Math.Abs(accelValue - AxRange.Min) > 10)
                        //    await _smartPort.Go(menuOption: CommandType.SetAx);
                        //else
                        //    await _smartPort.Go(menuOption: CommandType.MinAx);
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 1 when _isTip:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                            default:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                        }
                        PrintCollectResponse(axSensors, SensorType.Accelerometer);
                    }

                    while (axSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                        .All(x => x > AxRange.Max))
                    //if (accelValue > AxRange.Max)
                    {
                        saveRequire = true;
                        //var loopCount = (Convert.ToInt32(accelValue) - AxRange.Max) / _accelIncDecInterval;
                        //var reminder = Convert.ToInt32(accelValue) % AxRange.Max;
                        //if (reminder > 0)
                        //    loopCount++;
                        //bool singleIncrement = false;
                        //if (Math.Abs(AxRange.Min - Convert.ToInt32(accelValue)) <= _accelIncDecInterval
                        //    || Math.Abs(AxRange.Max - Convert.ToInt32(accelValue)) <= _accelIncDecInterval)
                        //    singleIncrement = true;

                        //if (loopCount > 0 && !singleIncrement)
                        //{
                        //    while (loopCount > 0)
                        //    {
                        //        _smartPort.Go(menuOption: CommandType.MaxAx).Wait();
                        //        loopCount--;
                        //    }
                        //}
                        //else
                        {
                            await _smartPort.Go(menuOption: CommandType.MaxAx);
                        }
                        //if (Math.Abs(accelValue - AxRange.Max) > 10)
                        //    await _smartPort.Go(menuOption: CommandType.SetAx);
                        //else
                        //    await _smartPort.Go(menuOption: CommandType.MaxAx);
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 1 when _isTip:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                            default:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                        }
                        PrintCollectResponse(axSensors, SensorType.Accelerometer);
                    }

                    if (axSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                            .All(x => x <= AxRange.Max)
                        && axSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                            .All(x => x >= AxRange.Min))
                    //if (accelValue <= AxRange.Max
                    //    && accelValue >= AxRange.Min && saveRequire)
                    {
                        await _smartPort.Go(menuOption: CommandType.SaveAx);
                        PrintCollectResponse(axSensors, SensorType.Accelerometer);
                        isBalanced = true;
                    }
                    else if (!saveRequire)
                    {
                        isBalanced = true;
                        PrintCollectResponse(axSensors, SensorType.Accelerometer);
                    }
                }
                Balanced(SensorType.Accelerometer);
                MemsTest();
            }
        }

        private static void MemsTest()
        {
            _defaultDataPortConfig.ModeFlag = new byte[] { 0x41 };
            _smartPort.WriteDataPortConfig(_boardId, _readDataPortConfig, _defaultDataPortConfig);
            List<Sensor> axSensors = new List<Sensor>();
            Task<LoopResponse> portTask;
            var testPassed = false;
            var completeCycleQuantz = new List<List<double>>();
            _options.MemsTestCycle += 2;
            for (int i = 0; i < _options.MemsTestCycle; i++)
            {
                var cycleQuantz = new List<double>();
                portTask = _smartPort.Go(menuOption: CommandType.Collect);
                switch (_options.Model)
                {
                    case 1 when _isTip:
                        axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                            .Where(x => x.Type == SensorType.Accelerometer).ToList();
                        break;
                    case 1 when !_isTip:
                        axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Take(2)
                            .Where(x => x.Type == SensorType.Accelerometer).ToList();
                        break;
                }
                if (i < 2)
                    continue;
                foreach (var sensor in axSensors.Where(x => _isTip ? x.Afe == Afe.Tip : x.Afe == Afe.Top))
                {
                    //var quantized = Math.Truncate(sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x));
                    //var nonQuantized = Math.Truncate(sensor.Data.Average(x => x.Value));
                    foreach (var item in sensor.Data)
                    {
                        var quantized = BitConverter.ToUInt16(item.Bytes, 0);
                        var nonQuantized = Math.Truncate(item.Value);
                        //SmartLog.WriteLine(
                        //    $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{nonQuantized}," +
                        //    $" Quantized: {quantized}");
                        cycleQuantz.Add(quantized);
                    }
                    //var allQuants = sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).ToList();
                    //var thisMin = Math.Truncate(allQuants.OrderBy(x => x).Take(20).Average(x => x));
                    //var thisMax = Math.Truncate(allQuants.OrderBy(x => x).Skip(20).Average(x => x));
                    //cycleQuantz.Add(thisMin);
                    //cycleQuantz.Add(thisMax);
                }
                completeCycleQuantz.Add(cycleQuantz);
            }
            var min = completeCycleQuantz != null && completeCycleQuantz.Any()
                ? completeCycleQuantz.Where(x=>x != null).Min(x => x.Min())
                : 0;
            var max = completeCycleQuantz != null && completeCycleQuantz.Any()
                ? completeCycleQuantz.Where(x => x != null).Max(x => x.Max())
                : 0;

            if (min >= (AxRange.Min - _options.MemsTestMin) &&
                min <= (AxRange.Max + _options.MemsTestMax) &&

                max >= (AxRange.Min - _options.MemsTestMin) &&
                max <= (AxRange.Max + _options.MemsTestMax))
            {
                if (_options.MemsTestMin <= (max - min)
                    && _options.MemsTestMax >= (max - min))
                {
                    testPassed = true;
                }
                if (!testPassed)
                    SmartLog.WriteErrorLine($"Mems Test Failed: Desired Range {_options.MemsTestMin}-{_options.MemsTestMax}, actual value:{max - min}");
                else
                    SmartLog.WriteHighlight($"Mems Test PASSED: Desired Range {_options.MemsTestMin}-{_options.MemsTestMax}, actual value:{max - min}");
            }
            else
            {
                SmartLog.WriteErrorLine($"Mems Test Failed: Ax Balance is out of range {min}-{max}. Please consider to rebalance this AFE");
            }

            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }
            var data = File.ReadAllText(filePath);
            var balancingData = !string.IsNullOrWhiteSpace(data)
                     ? JsonConvert.DeserializeObject<BalanceSensor>(data)
                     : new BalanceSensor();
            if (_isTip)
            {
                if (_readDataPortConfig.SensorChannels[2].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                {
                    balancingData.ChannelOne.ChannelName = SensorType.StrainGauge.ToString();
                }
                else
                {
                    balancingData.ChannelOne.ChannelName = SensorType.Accelerometer.ToString();
                }
                if (_readDataPortConfig.SensorChannels[3].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                {
                    balancingData.ChannelTwo.ChannelName = SensorType.StrainGauge.ToString();
                }
                else
                {
                    balancingData.ChannelTwo.ChannelName = SensorType.Accelerometer.ToString();
                }
            }
            else
            {
                if (_readDataPortConfig.SensorChannels[0].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                {
                    balancingData.ChannelOne.ChannelName = SensorType.StrainGauge.ToString();
                }
                else
                {
                    balancingData.ChannelOne.ChannelName = SensorType.Accelerometer.ToString();
                }
                if (_readDataPortConfig.SensorChannels[1].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                {
                    balancingData.ChannelTwo.ChannelName = SensorType.StrainGauge.ToString();
                }
                else
                {
                    balancingData.ChannelTwo.ChannelName = SensorType.Accelerometer.ToString();
                }
            }
            if (balancingData.ChannelOne.ChannelName == SensorType.Accelerometer.ToString())
            {
                completeCycleQuantz.ForEach(x =>
                {
                    var testIteration = new TestIteration();
                    testIteration.Reading.Add(x.Min());
                    testIteration.Reading.Add(x.Max());
                    balancingData.ChannelOne.MemsTest.TestIteration.Add(testIteration);
                    balancingData.ChannelOne.MemsTest.IsPass = testPassed;
                });
            }
            if (balancingData.ChannelTwo.ChannelName == SensorType.Accelerometer.ToString())
            {
                completeCycleQuantz.ForEach(x =>
                {
                    var testIteration = new TestIteration();
                    testIteration.Reading.Add(x.Min());
                    testIteration.Reading.Add(x.Max());
                    balancingData.ChannelTwo.MemsTest.TestIteration.Add(testIteration);
                    balancingData.ChannelOne.MemsTest.IsPass = testPassed;
                });
            }

            var balancingInString = JsonConvert.SerializeObject(balancingData);
            File.WriteAllText(filePath, balancingInString);
            _defaultDataPortConfig.ModeFlag = new byte[] { 0x42 };
            _smartPort.WriteDataPortConfig(_boardId, _readDataPortConfig, _defaultDataPortConfig);
        }

        private static async Task AdjustStrain(DataPortConfig readDataPortConfig, List<Sensor> sensors)
        {
            if (readDataPortConfig.SensorChannels.Any(x =>
                x.Type.ToDecimal(0) == (int) SensorType.StrainGauge))
            {
                var strainSensors = sensors.Where(x =>
                    x.Afe == (_isTip? Afe.Tip: Afe.Top)
                    && x.Type == SensorType.StrainGauge).ToList();
                var isBalanced = false;
                var saveRequire = false;
                while (!isBalanced)
                {
                    //while (strainSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //    .All(x => x < StrainRange.Min))

                    var strainValues = strainSensors.Select(s =>
                            Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0)))
                                .Average(b => b))).ToList();
                    //var strainValues = strainSensors.Select(s =>
                    //      Math.Truncate(s.Data.Average(x => x.Value))).ToList();

                    Task <LoopResponse> portTask;
                    //if (strainValues.All(x=>x < StrainRange.Min))

                    while (strainSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                        .All(x => x < StrainRange.Min))
                    {
                        //var maxStrain = strainValues.Any()
                        //    ? strainValues.Max()
                        //    : 0;
                        //saveRequire = true;
                        //var loopCount = ((StrainRange.Min - 50) - Convert.ToInt32(maxStrain)) / _strainIncDecInterval;
                        //var reminder = maxStrain == 0 
                        //    ? 0 
                        //    :(StrainRange.Min - 50) % Convert.ToInt32(maxStrain);
                      
                        //if (reminder > 0)
                        //    loopCount++;
                        //bool singleIncrement = false;
                        //if (Math.Abs(StrainRange.Min - Convert.ToInt32(maxStrain)) <= _strainIncDecInterval
                        //    || Math.Abs(StrainRange.Max - Convert.ToInt32(maxStrain)) <= _strainIncDecInterval)
                        //    singleIncrement = true;

                        //if (loopCount > 0 && !singleIncrement)
                        //{
                        //    while (loopCount > 0)
                        //    {
                        //       var response =  _smartPort.Go(menuOption: CommandType.IncrementSg).Result;
                        //        if ((int)response.ReturnObject != 0)
                        //            break;
                        //        loopCount--;
                        //    }
                        //}
                        //else
                        {
                            await _smartPort.Go(menuOption: CommandType.IncrementSg);
                        }
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 2 when _isTip:
                            case 1 when _isTip:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                            //case 2 when !_isTip:
                            default:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                        }
                        PrintCollectResponse(strainSensors, SensorType.StrainGauge);
                    }

                    while (strainSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                        .All(x => x > StrainRange.Max))
                    //if (strainValues.All(x => x > StrainRange.Max))
                    {
                        //var maxStrain = strainValues.Any()
                        //    ? strainValues.Max()
                        //    : 0;

                        //saveRequire = true;
                        //var loopCount = (Convert.ToInt32(maxStrain) - (StrainRange.Max-50)) / _strainIncDecInterval;
                        //var reminder = Convert.ToInt32(maxStrain) % (StrainRange.Max - 50);
                        //if (reminder > 0)
                        //    loopCount++;
                        //bool singleIncrement = false;
                        //if (Math.Abs(StrainRange.Min - Convert.ToInt32(maxStrain)) <= _strainIncDecInterval
                        //    || Math.Abs(StrainRange.Max - Convert.ToInt32(maxStrain)) <= _strainIncDecInterval)
                        //    singleIncrement = true;

                        //if (loopCount > 0 && !singleIncrement)
                        //{
                        //    while (loopCount > 0)
                        //    {
                        //        _smartPort.Go(menuOption: CommandType.DecrementSg).Wait();
                        //        loopCount--;
                        //    }
                        //}
                        //else
                        {
                            await _smartPort.Go(menuOption: CommandType.DecrementSg);
                        }
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 2 when _isTip:
                            case 1 when _isTip:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                            //case 2 when !_isTip:
                            default:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                        }
                        PrintCollectResponse(strainSensors, SensorType.StrainGauge);
                    }

                    if (strainSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                            .All(x => x <= StrainRange.Max)
                        && strainSensors.Select(s => Math.Truncate(s.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x)))
                            .All(x => x >= StrainRange.Min))
                    //if (strainValues.All(x => x <= StrainRange.Max)
                    //    && strainValues.All(x => x >= StrainRange.Min) && saveRequire)
                    {
                        await _smartPort.Go(menuOption: CommandType.SaveSg);
                        PrintCollectResponse(strainSensors, SensorType.StrainGauge);
                        isBalanced = true;
                    }
                    else if (!saveRequire)
                    {
                        isBalanced = true;
                        PrintCollectResponse(strainSensors, SensorType.StrainGauge);
                    }
                }
               
                Balanced(SensorType.StrainGauge);
            }
        }

        private static void Balanced(SensorType type)
        {
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }
            var data = File.ReadAllText(filePath);
            var balancingData = !string.IsNullOrWhiteSpace(data)
                     ? JsonConvert.DeserializeObject<BalanceSensor>(data)
                     : new BalanceSensor();
            if (balancingData.ChannelOne.ChannelName == type.ToString())
                balancingData.ChannelOne.IsCompleted = true;
            if (balancingData.ChannelTwo.ChannelName == type.ToString())
                balancingData.ChannelTwo.IsCompleted = true;
            var balancingInString = JsonConvert.SerializeObject(balancingData);
            File.WriteAllText(filePath, balancingInString);
        }

        private static void PrintCollectResponse(List<Sensor> sensors, SensorType type)
        {
            ConsoleColor borderColor = ConsoleColor.Yellow;
            Console.ForegroundColor = borderColor;
            //foreach (var sensor in sensors
            //   .Where(x => (type == SensorType.Both || x.Type == type)))
            //{
            //    var quantized = Math.Truncate(sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x));
            //    SmartLog.WriteLine(
            //        $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{Math.Truncate(sensor.Data.Average(x => x.Value))}," +
            //        $" Quantized: {quantized}");
            //}
            foreach (var sensor in sensors
            .Where(x => x.Afe == (_isTip ? Afe.Tip : Afe.Top)
            && (type == SensorType.Both || x.Type == type)))
            {
                var quantized = Math.Truncate(sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x));
                SmartLog.WriteLine(
                    $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{Math.Truncate(sensor.Data.Average(x => x.Value))}," +
                    $" Quantized: {quantized}");

                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Close();
                }
                var data = File.ReadAllText(filePath);
                var balancingData = !string.IsNullOrWhiteSpace(data)
                         ? JsonConvert.DeserializeObject<BalanceSensor>(data)
                         : new BalanceSensor();
                if (_isTip)
                {
                    if (_readDataPortConfig.SensorChannels[2].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                    {
                        balancingData.ChannelOne.ChannelName = SensorType.StrainGauge.ToString();
                    }
                    else
                    {
                        balancingData.ChannelOne.ChannelName = SensorType.Accelerometer.ToString();
                    }
                    if (_readDataPortConfig.SensorChannels[3].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                    {
                        balancingData.ChannelTwo.ChannelName = SensorType.StrainGauge.ToString();
                    }
                    else
                    {
                        balancingData.ChannelTwo.ChannelName = SensorType.Accelerometer.ToString();
                    }
                }
                else
                {
                    if (_readDataPortConfig.SensorChannels[0].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                    {
                        balancingData.ChannelOne.ChannelName = SensorType.StrainGauge.ToString();
                    }
                    else
                    {
                        balancingData.ChannelOne.ChannelName = SensorType.Accelerometer.ToString();
                    }
                    if (_readDataPortConfig.SensorChannels[1].Type.ToDecimal(0) == (int)SensorType.StrainGauge)
                    {
                        balancingData.ChannelTwo.ChannelName = SensorType.StrainGauge.ToString();
                    }
                    else
                    {
                        balancingData.ChannelTwo.ChannelName = SensorType.Accelerometer.ToString();
                    }
                }
                if (balancingData.ChannelOne.ChannelName == type.ToString())
                    balancingData.ChannelOne.Reading.Add(quantized);
                if (balancingData.ChannelTwo.ChannelName == type.ToString())
                    balancingData.ChannelTwo.Reading.Add(quantized);
                if (type.ToString() == SensorType.Both.ToString())
                {
                    //if(balancingData.ChannelOne.ChannelName == sensor.Type.ToString())
                    //    balancingData.ChannelOne.Reading.Add(quantized);
                    //if (balancingData.ChannelTwo.ChannelName == sensor.Type.ToString())
                    //    balancingData.ChannelTwo.Reading.Add(quantized);
                }
                var balancingInString = JsonConvert.SerializeObject(balancingData);
                File.WriteAllText(filePath, balancingInString);
            }

            borderColor = ConsoleColor.White;
            Console.ForegroundColor = borderColor;
        }

        private static void CompareDataPortConfig(DataPortConfig dataPortConfig, DataPortConfig defaultDataPortConfig)
        {
            var isValid = true;
            if (!dataPortConfig.ActChannelsDataPacking.SequenceEqual(defaultDataPortConfig.ActChannelsDataPacking))
            {
                isValid = false;
                SmartLog.WriteErrorLine(
                    $"Act Channel Data Pack {dataPortConfig.ActChannelsDataPacking.ToHex()} not matching with default {defaultDataPortConfig.ActChannelsDataPacking.ToHex()}");
            }

            if (!dataPortConfig.SampleInterval.SequenceEqual(defaultDataPortConfig.SampleInterval))
            {
                isValid = false;
                SmartLog.WriteErrorLine(
                    $"Sample Interval {dataPortConfig.SampleInterval.ToHex()} not matching with default {defaultDataPortConfig.SampleInterval.ToHex()}");
            }

            //if (!dataPortConfig.FirmwareVersion.SequenceEqual(defaultDataPortConfig.FirmwareVersion))
            //{
            //    isValid = false;
            //    SmartLog.WriteErrorLine(
            //        $"Firmware Version {dataPortConfig.FirmwareVersion.ToHex()} not matching with default {defaultDataPortConfig.FirmwareVersion.ToHex()}");
            //}
            if (!dataPortConfig.ModeFlag.SequenceEqual(defaultDataPortConfig.ModeFlag))
            {
                isValid = false;
                SmartLog.WriteErrorLine(
                    $"Mode Flag {dataPortConfig.ModeFlag.ToHex()} not matching with default {defaultDataPortConfig.ModeFlag.ToHex()}");
            }
            if (!isValid)
            {
                _smartPort.WriteDataPortConfig(_boardId, dataPortConfig, defaultDataPortConfig);
            }
        }

        private static void Greet()
        {
            ConsoleColor BorderColor = ConsoleColor.Cyan;
            Console.ForegroundColor = BorderColor;
            SmartLog.WriteLine(" Analog Front End (AFE) Calibration");
            SmartLog.WriteLine("--------------------------------------------------------");
            SmartLog.WriteLine("--------------------------------------------------------");
            //SmartLog.WriteToEvent($"Greeting - {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss")}");
        }

        private static void GetBoardId()
        {
            if (!string.IsNullOrWhiteSpace(_options.BoardId) && _options.BoardId.HexToByteArray() != null)
            {
                _boardId = _options.BoardId.HexToByteArray()[0];
                return;
            }
            SmartLog.WriteLine("\nPlease Enter Interface Board Id (in Hex):");
            bool isValidBoardId = false;
            while (!isValidBoardId)
            {
                var bytes = Console.ReadLine().HexToByteArray();
                if (bytes == null)
                {
                    SmartLog.WriteLine("\nInvalid Hex. Please re-enter");
                }
                else
                {
                    _boardId = bytes[0];
                    isValidBoardId = true;
                }
            }
        }

        private static bool DetectComPort()
        {
            _defaultComPort = _smartPort.GetPort("0403", "6001");
            if (string.IsNullOrWhiteSpace(_defaultComPort))
            {
                SmartLog.WriteLine("Available COM ports");
                SmartLog.WriteLine("--------------------------------------------------------");

                int i = 1;
                int selectedOption;
                var comports = ComPortInfo.GetComPortsInfo();
                comports.ForEach(x =>
                {
                    SmartLog.WriteLine($"{i}) {x.Name}:{x.Description}");
                    i++;
                });
                SmartLog.WriteLine("\nSelect COM Port (Example: Type 1 and press enter for first COM port):");
                while (!int.TryParse(Console.ReadLine(), out selectedOption)
                       || (comports.ElementAtOrDefault(selectedOption - 1) == null))
                {
                    SmartLog.WriteLine("Invalid Entry\nPlease Enter from available options");
                }

                var selectedComPort = comports.ElementAtOrDefault(selectedOption - 1);
                if (selectedComPort == null)
                {
                    SmartLog.WriteLine("Invalid Entry\nPlease Enter from available options");
                    Console.ReadKey();
                    return false;
                }

                _defaultComPort = selectedComPort.Name;
            }

            return true;
        }

        private static void Init()
        {
            if (File.Exists(filePath))
            {
                File.WriteAllText(filePath, "");
            }
            ConsoleColor BorderColor = ConsoleColor.White;
            Console.ForegroundColor = BorderColor;
            _smartPort = new SmartPort();
            //SmartLog.WriteToEvent($"Init - {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss")}");
        }
    }
}