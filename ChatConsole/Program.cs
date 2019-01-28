using System;
using System.Linq;
using System.Threading;
using Smart.Agent.Business;
using Smart.Agent.Helper;
using Smart.Hid;

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
            Console.WriteLine("Available COM ports\n");
            Console.WriteLine("--------------------------------------------------------\n");
            var comports = ComPortInfo.GetComPortsInfo();
            int i = 1;
            int selectedOption;
            comports.ForEach(x =>
            {
                Console.WriteLine($"{i}) {x.Name}:{x.Description}");
                i++;
            });
            Console.WriteLine("\n");
            Console.WriteLine("Select COM Port (Example: Type 1 and press enter for first COM port)\n");
            while (!int.TryParse(Console.ReadLine(), out selectedOption)
                   || (comports.ElementAtOrDefault(selectedOption - 1) == null))
            {
                Console.WriteLine("Invalid Entry\nPlease Enter from available options");
            }

            var selectedComPort = comports.ElementAtOrDefault(selectedOption - 1);
            if (selectedComPort == null)
            {
                Console.WriteLine("Invalid Entry\nPlease Enter from available options");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Please Enter Interface Board Id (in Hex)\n");
            while (!StringToByteArray(Console.ReadLine()))
            {
                Console.WriteLine($"Invalid Hex. Please re-enter\n");
            }


            SmartPort smartPort = new SmartPort();
            smartPort.Init(_boardId, selectedComPort.Name);
            smartPort.Start();
            smartPort.Collect();

            _smartDevice = new SmartDevice();
            _smartDevice.Init();
            var task = _smartDevice.Go(menuOption: _smartDevice.Menu());
            new ManualResetEvent(false).WaitOne();


            Console.WriteLine("Type exit to quite\n");
            while (!Convert.ToString(Console.ReadLine()).Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Unknown command\n");
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
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}