using System;
using System.Linq;
using Smart.Agent.Business;

namespace ChatConsole
{
    class SerialPortProgram
    {
        private static byte _boardId;

        [STAThread]
        static void Main(string[] args)
        {
            //ComPortInfo.GetComPortsInfo().ForEach(x => { Console.WriteLine($"{x.Name}:{x.Description}"); });
            Console.WriteLine("Please Enter Interface Board Id (in Hex)\n");
            while (!StringToByteArray(Console.ReadLine()))
            {
                Console.WriteLine($"Invalid Hex. Please re-enter\n");
            }

            SmartPort smartPort = new SmartPort();
            smartPort.Init(_boardId, "COM6");
            smartPort.Start();
            Console.ReadKey();
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