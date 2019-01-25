using System;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Smart.Agent.Helper;
using Smart.Agent.Model;

namespace Smart.Agent.Business
{
    public class SmartPort
    {
        private SerialPort _port;
        private string _comPortName;
        private Queue _commaQueue;
        private void HandleSerialData(byte[] respBuffer)
        {
            var hexString = ByteArrayToString(respBuffer);
            Console.WriteLine(hexString);
        }

        private static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        private void port_DataReceived(object sender,
            SerialDataReceivedEventArgs e)
        {
            // Show all the incoming data in the port's buffer
            int bytes = _port.BytesToRead;
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
                var command = queue.CommBytes;
                while (queue.MaxRetry >= 1)
                {
                    Console.WriteLine($"***** Sending {queue.CommandName} Command *****");
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

            Console.WriteLine("--- Completed the Command Queue ---");
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
    }
}