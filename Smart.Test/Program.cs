using CommandLine;
using Smart.Agent.Business;
using Smart.Agent.Constant;
using Smart.Agent.Helper;
using Smart.Agent.Model;
using Smart.Log;
using Smart.Log.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Smart.Test
{
    class Program
    {
        private static SmartPort _smartPort;
        private static byte _boardId;
        private static Options _options;
        private static string _defaultComPort;

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
                    SmartLog.WriteLine(o.Model == 1 || o.Model == 2
                        ? $"Entered Model. Current Arguments: --mdl {o.Model + (o.Model == 1 ? ":Ax-Sg" : ":Sg-Sg")}"
                        : $"No Model entered. Current Arguments: --mdl");
                    SmartLog.WriteLine(!string.IsNullOrWhiteSpace(o.Command)
                       ? $"Entered Command. Current Arguments: --cmd {o.Command.ToUpper()}"
                       : $"No Command passed. Current Arguments: --cmd");
                });

            if (args != null && args.Any() && args[0].Equals("--help", StringComparison.InvariantCultureIgnoreCase))
                return;
            if (args != null && args.Any() && args[0].Equals("--version", StringComparison.InvariantCultureIgnoreCase))
                return;
            Init();
            if (!DetectComPort())
                return;
            GetBoardId();
            Console.WriteLine();
            DataPortConfig defaultDataPortConfig = _smartPort.GetDefaultDataPortConfig();
            await _smartPort.Init(_boardId, _defaultComPort, _options);
            if (!_smartPort.Start())
                return;
            var loopStartDateTime = DateTime.Now;
            var loopCounter = 1;
            List<DateTime> outageLoopCounter = new List<DateTime>();
            while (true)
            {
                var portTask = _smartPort.Go(menuOption: CommandType.Collect);
                if (portTask.Result.ReturnObject != null)
                {
                    var sensors = ((List<Sensor>)portTask.Result.ReturnObject);
                    PrintCollectResponse(sensors, SensorType.Both);
                }
                else
                {
                    outageLoopCounter.Add(DateTime.Now);
                }
                if(outageLoopCounter.Count > 50)
                {
                    SmartLog.WriteErrorLine("No response for collect command");
                    var message = $"Continous Collect ran for { DateTime.Now.Subtract(loopStartDateTime).TotalMinutes} minutes, startign from {loopStartDateTime.ToString("mm/dd/yyyy hh:mm")} to {DateTime.Now.ToString("mm/dd/yyyy hh:mm")}";
                    message = message + "" + $"Total Consecutive Collects: {loopCounter}";
                    message = message + "" + string.Join(" | ", outageLoopCounter.Select(x => x.ToString("mm/dd/yyyy hh:mm:ss")));
                    message = message + "" + $"Any Error Info";

                    // Send Email to te@smart-strcutures.com
                    SmartLog.WriteErrorLine(message);
                    loopStartDateTime = DateTime.Now;
                    loopCounter = 1;
                    break;
                }
                loopCounter++;
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }
        private static void Init()
        {
            ConsoleColor BorderColor = ConsoleColor.White;
            Console.ForegroundColor = BorderColor;
            _smartPort = new SmartPort();
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
        private static void PrintCollectResponse(List<Sensor> sensors, SensorType type)
        {
            ConsoleColor borderColor = ConsoleColor.Yellow;
            Console.ForegroundColor = borderColor;
            foreach (var sensor in sensors)
            {
                var quantized = Math.Truncate(sensor.Data.Select(x => BitConverter.ToUInt16(x.Bytes, 0)).Average(x => x));
                SmartLog.WriteLine(
                   $"{sensor.Afe} ({sensor.Data.Count} samples): {sensor.Type}:{Math.Truncate(sensor.Data.Average(x => x.Value))}," +
                   $" Quantized: {quantized}");
            }
            borderColor = ConsoleColor.White;
            Console.ForegroundColor = borderColor;
        }
    }
}
