using System;
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

namespace FinalCal
{
    class Program
    {
        private static byte _boardId;
        private static SmartPort _smartPort;
        private static string _defaultComPort;
        private static Options _options;

        [STAThread]
        // ReSharper disable once UnusedParameter.Local
        static async Task Main(string[] args)
        {
            Console.Clear();
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    _options = o;
                    SmartLog.WriteLine(o.PrintRequest
                        ? $"Print Request enabled. Current Arguments: --req {o.PrintRequest}"
                        : $"Print Request disabled. Current Arguments: --req {o.PrintRequest}");
                    SmartLog.WriteLine(o.PrintResponse
                        ? $"Print Response enabled. Current Arguments: --res {o.PrintResponse}"
                        : $"Print Response disabled. Current Arguments: --res {o.PrintResponse}");
                    SmartLog.WriteLine(o.PrintDataPortConfig
                        ? $"Print Data Port Config enabled. Current Arguments: --conf {o.PrintDataPortConfig}"
                        : $"Print Data Port Config disabled. Current Arguments: --conf {o.PrintDataPortConfig}");
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
            CompareDataPortConfig(readDataPortConfig, defaultDataPortConfig);
            var portTask = _smartPort.Go(menuOption: CommandType.Collect);
            while (portTask.Result.Selection != CommandType.PowerOff)
            {
                switch (portTask.Result.Selection)
                {
                    case CommandType.Collect:
                    {
                        if (portTask.Result.ReturnObject != null)
                        {
                            ConsoleColor borderColor = ConsoleColor.Yellow;
                            Console.ForegroundColor = borderColor;
                            foreach (var sensor in (List<Sensor>) portTask.Result.ReturnObject)
                            {
                                //if (sensor.Data != null && sensor.Data.Any())
                                    SmartLog.WriteLine(
                                        $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{Math.Truncate(sensor.Data.Average(x => x.Value))}");
                            }

                            borderColor = ConsoleColor.White;
                            Console.ForegroundColor = borderColor;
                        }

                        break;
                    }
                    case CommandType.ReadDataPort:
                    {
                        if (portTask.Result.ReturnObject != null)
                        {
                            var dataPortConfig = ((DataPortConfig)portTask.Result.ReturnObject);
                            CompareDataPortConfig(dataPortConfig, defaultDataPortConfig);
                        }

                        break;
                    }
                }

                portTask = _smartPort.Go(menuOption: _smartPort.Menu());
            }

            SmartLog.WriteLine("Type exit to quite\n");
            while (!Convert.ToString(Console.ReadLine()).Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                SmartLog.WriteLine("Unknown command\n");
            }

            //_smartPort.PowerOff();
        }

        private static void CompareDataPortConfig(DataPortConfig dataPortConfig, DataPortConfig defaultDataPortConfig)
        {
            var isValid = true;
            if (!dataPortConfig.ActChannelsDataPacking.SequenceEqual(defaultDataPortConfig.ActChannelsDataPacking))
            {
                isValid = false;
                SmartLog.WriteLine(
                    $"Act Channel Data Pack {dataPortConfig.ActChannelsDataPacking.ToHex()} not matching with default {defaultDataPortConfig.ActChannelsDataPacking.ToHex()}");
            }

            if (!dataPortConfig.SampleInterval.SequenceEqual(defaultDataPortConfig.SampleInterval))
            {
                isValid = false;
                SmartLog.WriteLine(
                    $"Sample Interval {dataPortConfig.SampleInterval.ToHex()} not matching with default {defaultDataPortConfig.SampleInterval.ToHex()}");
            }

            if (!dataPortConfig.FirmwareVersion.SequenceEqual(defaultDataPortConfig.FirmwareVersion))
            {
                isValid = false;
                SmartLog.WriteLine(
                    $"Firmware Version {dataPortConfig.FirmwareVersion.ToHex()} not matching with default {defaultDataPortConfig.FirmwareVersion.ToHex()}");
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
            SmartLog.WriteLine("\nSmartPile AFE Bridge Balancing (Final CAL)");
            SmartLog.WriteLine("--------------------------------------------------------");
            SmartLog.WriteLine("--------------------------------------------------------");
        }

        private static void GetBoardId()
        {
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