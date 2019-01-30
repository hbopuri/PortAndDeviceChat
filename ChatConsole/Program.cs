using System;
using System.Linq;
using System.Threading;
using Smart.Agent.Business;
using Smart.Agent.Helper;
using Smart.Hid;
using Smart.Log;

namespace ChatConsole
{
    class Program
    {
        private static byte _boardId;
        private static SmartDevice _smartDevice;
        [STAThread]
        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
            Console.Clear();
            SmartLog.WriteLine("Available COM ports\n");
            SmartLog.WriteLine("--------------------------------------------------------\n");
            var comports = ComPortInfo.GetComPortsInfo();
            int i = 1;
            int selectedOption;
            comports.ForEach(x =>
            {
                SmartLog.WriteLine($"{i}) {x.Name}:{x.Description}");
                i++;
            });
            SmartLog.WriteLine("\n");
            SmartLog.WriteLine("Select COM Port (Example: Type 1 and press enter for first COM port)\n");
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
                return;
            }
            SmartLog.WriteLine("Please Enter Interface Board Id (in Hex)\n");
            while (!StringToByteArray(Console.ReadLine()))
            {
                SmartLog.WriteLine($"Invalid Hex. Please re-enter\n");
            }


            SmartPort smartPort = new SmartPort();
            smartPort.Init(_boardId, selectedComPort.Name);
            smartPort.Start();
            SmartLog.WriteLine("Press 1 to Collect");
            while (int.TryParse(Console.ReadLine(), out var reRun) && reRun==1)
            {
                var sensors = smartPort.Collect();
                foreach (var sensor in sensors)
                {
                    SmartLog.WriteLine($"{sensor.Type}:{sensor.Average} for {sensor.Data.Count} samples");
                }
                SmartLog.WriteLine("Press (1) to ReCollect (0) to move next");
            }

            _smartDevice = new SmartDevice();
            _smartDevice.Init();
            var task = _smartDevice.Go(menuOption: _smartDevice.Menu());
            new ManualResetEvent(false).WaitOne();


            SmartLog.WriteLine("Type exit to quite\n");
            while (!Convert.ToString(Console.ReadLine()).Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                SmartLog.WriteLine("Unknown command\n");
            }

            _smartDevice.Dispose();

            smartPort.PowerOff();
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