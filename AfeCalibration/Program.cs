﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
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

        [STAThread]
        // ReSharper disable once UnusedParameter.Local
        static async Task Main(string[] args)
        {
            Console.Clear();
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
                    SmartLog.WriteLine(o.Model == 1  || o.Model == 2
                        ? $"Entered Model. Current Arguments: --mdl {o.Model + (o.Model== 1 ? ":Ax-Sg": ":Sg-Sg")}"
                        : $"No Model entered. Current Arguments: --mdl");
                });

            if (args != null && args.Any() && args[0].Equals("--help", StringComparison.InvariantCultureIgnoreCase))
                return;
            if (args != null && args.Any() && args[0].Equals("--version", StringComparison.InvariantCultureIgnoreCase))
                return;

            Greet();
            Init();
            if (!DetectComPort())
                return;
            GetBoardId();
            Console.WriteLine();
            DataPortConfig defaultDataPortConfig = _smartPort.GetDefaultDataPortConfig();
            await _smartPort.Init(_boardId, _defaultComPort, _options);
            if (!_smartPort.Start())
                return;
            var readDataPortConfig = _smartPort.ReadDataPortConfig();
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
                            PrintCollectResponse(sensors);
                        }
                        break;
                    }
                    case CommandType.Collect:
                    {
                        if (portTask.Result.ReturnObject != null)
                        {
                            var sensors = ((List<Sensor>) portTask.Result.ReturnObject);
                            if (sensors[1].Data != null && sensors[1].Data.Any())
                            {
                                var channelThreeQuantized =
                                    //Math.Truncate(sensors[1].Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0))
                                    //    .Average(x => x));
                                Math.Truncate(sensors[1].Data.Average(x => x.Value));
                                _isTip = channelThreeQuantized < 300 || channelThreeQuantized > 3800;
                            }

                            PrintCollectResponse(sensors);
                            //if (_options.Model.Equals(2))
                            //    sensors = sensors.Skip(2).Take(2).ToList();
                            await AdjustStrain(readDataPortConfig, sensors);
                            await AdjustAx(readDataPortConfig, sensors);
                        }

                        break;
                    }
                    case CommandType.ReadDataPort:
                    {
                        if (portTask.Result.ReturnObject != null)
                        {
                            var dataPortConfig = ((DataPortConfig) portTask.Result.ReturnObject);
                            CompareDataPortConfig(dataPortConfig, defaultDataPortConfig);
                        }

                        break;
                    }
                }

                //portTask = _smartPort.Go(menuOption: _smartPort.Menu());
                portTask = _smartPort.Go(menuOption: _smartPort.Menu(_printMenu));
                _printMenu = false;
            }

            //SmartLog.WriteLine("Type exit to quite\n");
            //while (!Convert.ToString(Console.ReadLine()).Equals("exit", StringComparison.OrdinalIgnoreCase))
            //{
            //    SmartLog.WriteLine("Unknown command\n");
            //}

            //_smartPort.PowerOff();
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
                    Task<LoopResponse> portTask;
                    //while (axSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //    .All(x => x < AxRange.Min))
                    while (axSensors.Select(s =>
                            Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0)))
                                .Average(x => x)))
                        .All(x => x < AxRange.Min))
                    {
                        saveRequire = true;
                        await _smartPort.Go(menuOption: CommandType.MinAx);
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 2:
                                axSensors = ((List<Sensor>) portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                            case 1 when _isTip:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                            default:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(0).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                        }
                        PrintCollectResponse(axSensors);
                    }

                    //while (axSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //    .All(x => x > AxRange.Max))
                    while (axSensors.Select(s =>
                            Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0))).Average(x => x)))
                        .All(x => x > AxRange.Max))
                    {
                        saveRequire = true;
                        await _smartPort.Go(menuOption: CommandType.MaxAx);
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 2:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                            case 1 when _isTip:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                            default:
                                axSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(0).Take(2)
                                    .Where(x => x.Type == SensorType.Accelerometer).ToList();
                                break;
                        }
                        PrintCollectResponse(axSensors);
                    }

                    //if (axSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //        .All(x => x < AxRange.Max)
                    //    && axSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //        .All(x => x > AxRange.Min))
                    if (axSensors.Select(s =>
                                Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0))).Average(x => x)))
                            .All(x => x <= AxRange.Max)
                        && axSensors.Select(s =>
                                Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0))).Average(x => x)))
                            .All(x => x >= AxRange.Min) && saveRequire)
                    {
                        await _smartPort.Go(menuOption: CommandType.SaveAx);
                        isBalanced = true;
                    }
                    else if (!saveRequire)
                    {
                        isBalanced = true;
                    }
                }
            }
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
                    Task<LoopResponse> portTask;
                    while (strainSensors.Select(s =>
                            Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0)))
                                .Average(b => b)))
                        .All(x => x < StrainRange.Min))
                    {
                        saveRequire = true;
                        await _smartPort.Go(menuOption: CommandType.IncrementSg);
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 2:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                            case 1 when _isTip:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                            default:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(0).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                        }
                        PrintCollectResponse(strainSensors);
                    }

                    //while (strainSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //    .All(x => x > StrainRange.Max))
                    while (strainSensors.Select(s =>
                            Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0)))
                                .Average(x => x)))
                        .All(x => x > StrainRange.Max))
                    {
                        saveRequire = true;
                        await _smartPort.Go(menuOption: CommandType.DecrementSg);
                        portTask = _smartPort.Go(menuOption: CommandType.Collect);
                        switch (_options.Model)
                        {
                            case 2:
                                strainSensors = ((List<Sensor>) portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                            case 1 when _isTip:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(2).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                            default:
                                strainSensors = ((List<Sensor>)portTask.Result.ReturnObject).Skip(0).Take(2)
                                    .Where(x => x.Type == SensorType.StrainGauge).ToList();
                                break;
                        }
                        PrintCollectResponse(strainSensors);
                    }

                    //if (strainSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //        .All(x => x < StrainRange.Max)
                    //    && strainSensors.Select(s => Math.Truncate(s.Data.Average(x => x.Value)))
                    //        .All(x => x > StrainRange.Min))
                    if (strainSensors.Select(s =>
                                Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0))).Average(x => x)))
                            .All(x => x <= StrainRange.Max)
                        && strainSensors.Select(s =>
                                Math.Truncate(s.Data.Select(x => Convert.ToInt16(BitConverter.ToUInt16(x.Bytes, 0))).Average(x => x)))
                            .All(x => x >= StrainRange.Min) && saveRequire)
                    {
                        await _smartPort.Go(menuOption: CommandType.SaveSg);
                        isBalanced = true;
                    }
                    else if (!saveRequire)
                    {
                        isBalanced = true;
                    }
                }
            }
        }

        private static void PrintCollectResponse(List<Sensor> sensors)
        {
            ConsoleColor borderColor = ConsoleColor.Yellow;
            Console.ForegroundColor = borderColor;
            foreach (var sensor in sensors)
            {
                SmartLog.WriteLine(
                    $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{Math.Truncate(sensor.Data.Average(x => x.Value))}," +
                    $" Quantized: {Math.Truncate(sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x))}");
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

            if (!dataPortConfig.FirmwareVersion.SequenceEqual(defaultDataPortConfig.FirmwareVersion))
            {
                isValid = false;
                SmartLog.WriteErrorLine(
                    $"Firmware Version {dataPortConfig.FirmwareVersion.ToHex()} not matching with default {defaultDataPortConfig.FirmwareVersion.ToHex()}");
            }
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
            ConsoleColor BorderColor = ConsoleColor.White;
            Console.ForegroundColor = BorderColor;
            _smartPort = new SmartPort();
        }
    }
}