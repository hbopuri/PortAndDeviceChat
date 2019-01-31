using System;
using System.Collections.Generic;
using System.Linq;
using Smart.Agent.Business;
using Smart.Agent.Helper;
using Smart.Agent.Model;
using Smart.Hid;
using Smart.Log;
using Smart.Log.Helper;

namespace ChatConsole
{
    class Program
    {
        private static byte _boardId;
        private static SmartDevice _smartDevice;
        private static SmartPort _smartPort;
        private static string _defaultComPort;

        [STAThread]
        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
            Console.Clear();
            DrawCircle(1);
            Greet();
            Init();
            if (!DetectComPort())
                return;
            GetBoardId();
            Console.WriteLine();
            _smartDevice.Init();
            _smartPort.Init(_boardId, _defaultComPort);
            _smartPort.Start();
            var portTask = _smartPort.Go(menuOption: _smartPort.Menu());
            if (portTask.Result.Selection == 1)
            {
                if (portTask.Result.ReturnObject != null)
                {
                    foreach (var sensor in (List<Sensor>) portTask.Result.ReturnObject)
                    {
                        if (sensor.Data != null && sensor.Data.Any())
                        {
                            SmartLog.WriteLine(
                                $"{sensor.Afe} ({sensor.Data.Count} samples):\n\tAccelerometer:{Math.Truncate(sensor.Data.Average(x => x.AccelerometerValue))}" +
                                $"\n\tStrain:{Math.Truncate(sensor.Data.Average(x => x.StrainValue))}");
                            //$"\n\t{Math.Truncate(sensor.Data.Average(x => x.AccelerometerValueLab))}(LabVIEW)");
                        }
                    }
                }
            }

            while (portTask.Result.Selection <= 2)
            {
                portTask = _smartPort.Go(menuOption: _smartPort.Menu());
                if (portTask.Result.Selection == 1) //Collect Command
                {
                    if (portTask.Result.ReturnObject != null)
                    {
                        foreach (var sensor in (List<Sensor>) portTask.Result.ReturnObject)
                        {
                            if (sensor.Data != null && sensor.Data.Any())
                            {
                                SmartLog.WriteLine(
                                    $"{sensor.Afe} ({sensor.Data.Count} samples):\n\tAccelerometer:{Math.Truncate(sensor.Data.Average(x => x.AccelerometerValue))}" +
                                    $"\n\tStrain:{Math.Truncate(sensor.Data.Average(x => x.StrainValue))}");
                                //$"\n\t{Math.Truncate(sensor.Data.Average(x => x.AccelerometerValueLab))}(LabVIEW)");
                            }
                        }
                    }
                }

                if (portTask.Result.Selection == 2) //Read Command
                {

                }

                if (portTask.Result.Selection >= 5)
                {
                    portTask = _smartPort.Go(menuOption: 1); //Collect
                    if (portTask.Result.ReturnObject != null)
                    {
                        foreach (var sensor in (List<Sensor>) portTask.Result.ReturnObject)
                        {
                            if (sensor.Data != null && sensor.Data.Any())
                            {
                                SmartLog.WriteLine(
                                    $"{sensor.Afe} ({sensor.Data.Count} samples):\n\tAccelerometer:{Math.Truncate(sensor.Data.Average(x => x.AccelerometerValue))}" +
                                    $"\n\tStrain:{Math.Truncate(sensor.Data.Average(x => x.StrainValue))}");
                                //$"\n\t{Math.Truncate(sensor.Data.Average(x => x.AccelerometerValueLab))}(LabVIEW)");
                            }
                        }
                    }
                }

            }

            SmartLog.WriteLine("Type exit to quite\n");
            while (!Convert.ToString(Console.ReadLine()).Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                SmartLog.WriteLine("Unknown command\n");
            }

            _smartDevice.Dispose();

            _smartPort.PowerOff();
        }
        public static void DrawEllipse(char c, int centerX, int centerY, int width, int height)
        {
            for (int i = 0; i < width; i++)
            {
                int dx = i - width / 2;
                int x = centerX + dx;

                int h = (int)Math.Round(height * Math.Sqrt(width * width / 4.0 - dx * dx) / width);
                for (int dy = 1; dy <= h; dy++)
                {
                    Console.SetCursorPosition(x, centerY + dy);
                    Console.Write(c);
                    Console.SetCursorPosition(x, centerY - dy);
                    Console.Write(c);
                }

                if (h >= 0)
                {
                    Console.SetCursorPosition(x, centerY);
                    Console.Write(c);
                }
            }
        }
        private static void DrawCircle(double radius)
        {
            //double radius;
            double thickness = 0.4;
            ConsoleColor BorderColor = ConsoleColor.Cyan;
            Console.ForegroundColor = BorderColor;
            char symbol = '*';
            //do
            //{
            //    Console.Write("Enter radius:::: ");
            //    if (!double.TryParse(Console.ReadLine(), out radius) || radius <= 0)
            //    {
            //        Console.WriteLine("radius have to be positive number");
            //    }
            //} while (radius <= 0);

            Console.WriteLine();
            double rIn = radius - thickness, rOut = radius + thickness;

            for (double y = radius; y >= -radius; --y)
            {
                for (double x = -radius; x < rOut; x += 0.5)
                {
                    double value = x * x + y * y;
                    if (value >= rIn * rIn && value <= rOut * rOut)
                    {
                        Console.Write(symbol);
                    }
                    else
                    {
                        Console.Write(" ");
                    }
                }

                Console.WriteLine();
            }
        }

        private static void Greet()
        {
            SmartLog.WriteLine("SmartPile AFE Bridge Balancing (Final CAL)");
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
                    SmartLog.WriteLine($"\nInvalid Hex. Please re-enter");
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
           
            _smartPort = new SmartPort();
            _smartDevice = new SmartDevice();
        }

        public static bool StringToByteArray(string hex)
        {
            try
            {
                byte[] bytes = Enumerable.Range(0, hex.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                    .ToArray();
                _boardId = bytes[0];
                return true;
            }
            catch (Exception e)
            {
                SmartLog.WriteLine(e.Message);
                return false;
            }
        }
    }
}