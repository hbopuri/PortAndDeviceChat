using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Hid.Net.Windows;
using Usb.Net.Windows;

namespace ChatConsole
{
    class Program
    {
        #region Fields
        private static readonly SmartTrezor DeviceConnectionExample = new SmartTrezor();
        #endregion

        #region Main
        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
            //Register the factory for creating Usb devices. This only needs to be done once.
            WindowsUsbDeviceFactory.Register();
            WindowsHidDeviceFactory.Register();

            DeviceConnectionExample.TrezorInitialized += _DeviceConnectionExample_TrezorInitialized;
            DeviceConnectionExample.TrezorDisconnected += _DeviceConnectionExample_TrezorDisconnected;

            var task = Go(menuOption: Menu());
            new ManualResetEvent(false).WaitOne();

        }

        private static async Task Go(int menuOption)
        {
            switch (menuOption)
            {
                case 1:
                    try
                    {
                        await DeviceConnectionExample.InitializeTrezorAsync();
                        await DisplayDataAsync();
                        DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                    Console.ReadKey();
                    break;
                case 2:
                    Console.Clear();
                    DisplayWaitMessage();
                    DeviceConnectionExample.StartListening();
                    break;
                case 3:
                    try
                    {
                        await DeviceConnectionExample.InitializeTrezorAsync();
                        await IncrementAsync();
                        DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                    Console.ReadKey();
                    break;
                case 4:
                    try
                    {
                        await DeviceConnectionExample.InitializeTrezorAsync();
                        await DecrementAsync();
                        DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                    Console.ReadKey();
                    break;
                case 5:
                    try
                    {
                       var ports =  SerialPort.GetPortNames();
                       Connect(ports[0]);
                        //await DeviceConnectionExample.InitializeTrezorAsync(0x0403, 0x6001);
                        //await IfCommandAsync();
                        //DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                    Console.ReadKey();
                    break;
            }
        }
        #endregion
        private static void Connect(string portName)
        {
            var port = new SerialPort(portName);
            if (!port.IsOpen)
            {
                port.BaudRate = 19200;
                port.Open();
                var writeBuffer = new byte[64];
                writeBuffer[0] = 0x1E;
                writeBuffer[1] = 0x03;
                writeBuffer[2] = 0x03;
                writeBuffer[4] = 0x24;
                WriteByte(port,writeBuffer);
            }
        }
        public static void WriteByte(SerialPort port, byte[] buffer)
        {
            port.Write(buffer, 0, buffer.Length);   
        }
        #region Event Handlers
        private static void _DeviceConnectionExample_TrezorDisconnected(object sender, EventArgs e)
        {
            Console.Clear();
            Console.WriteLine("Disconnected.");
            DisplayWaitMessage();
        }

        private static async void _DeviceConnectionExample_TrezorInitialized(object sender, EventArgs e)
        {
            try
            {
                Console.Clear();
                await DisplayDataAsync();
            }
            catch (Exception ex)
            {
                Console.Clear();
                Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region Private Methods
        private static int Menu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Console sample. This sample demonstrates either writing to the first found connected device, or listening for a device and then writing to it. If you listen for the device, you will be able to connect and disconnect multiple times. This represents how users may actually use the device.");
                Console.WriteLine();
                Console.WriteLine("1. Write To Connected Device");
                Console.WriteLine("2. Listen For Device");
                Console.WriteLine("3. Increment Connected Device");
                Console.WriteLine("4. Decrement Connected Device");
                Console.WriteLine("5. IFB IF Command");
                var consoleKey = Console.ReadKey();
                if (consoleKey.KeyChar == '1') return 1;
                if (consoleKey.KeyChar == '2') return 2;
                if (consoleKey.KeyChar == '3') return 3;
                if (consoleKey.KeyChar == '4') return 4;
                if (consoleKey.KeyChar == '5') return 5;
            }
        }

        private static async Task DisplayDataAsync()
        {
            var bytes = await DeviceConnectionExample.WriteAndReadFromDeviceAsync();
            Console.Clear();
            Console.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
        private static async Task IncrementAsync()
        {
            var bytes = await DeviceConnectionExample.WriteToIncrementAsync();
            Console.Clear();
            Console.WriteLine("Increment Command Passed");
            Console.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
        private static async Task DecrementAsync()
        {
            await DeviceConnectionExample.WriteToDecrementAsync();
            Console.WriteLine("Decrement Command Passed");
            //Console.Clear();
            //Console.WriteLine("Device connected. Output:");
            //DisplayData(bytes);
        }
        private static async Task IfCommandAsync()
        {

            //Create a buffer with 3 bytes (initialize)
            var writeBuffer = new byte[64];
            writeBuffer[0] = 0x1E;
            writeBuffer[1] = 0x03;
            writeBuffer[2] = 0x03;
            writeBuffer[4] = 0x24;
            var bytes = await DeviceConnectionExample.WriteToAsync(writeBuffer);
            Console.Clear();
            Console.WriteLine("IF Command. Output:");
            DisplayData(bytes);
        }

        private static void DisplayData(byte[] readBuffer)
        {
            Console.WriteLine(string.Join(" ", readBuffer));
            Console.ReadKey();
        }

        private static void DisplayWaitMessage()
        {
            Console.WriteLine("Waiting for device to be plugged in...");
        }
        #endregion
    }
}