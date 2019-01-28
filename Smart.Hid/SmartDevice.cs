using System;
using System.Threading.Tasks;
using Hid.Net.Windows;
using Usb.Net.Windows;

namespace Smart.Hid
{
    public class SmartDevice
    {
        #region Fields
        private static readonly SmartTrezor DeviceConnectionExample = new SmartTrezor();
        #endregion

        private static void DisplayData(byte[] readBuffer)
        {
            Console.WriteLine(string.Join(" ", readBuffer));
            Console.ReadKey();
        }
        private static async Task DisplayDataAsync()
        {
            var bytes = await DeviceConnectionExample.WriteAndReadFromDeviceAsync();
            Console.Clear();
            Console.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }

        private static void DisplayWaitMessage()
        {
            Console.WriteLine("Waiting for device to be plugged in...");
        }

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
        public async void Init()
        {
            //Register the factory for creating Usb devices. This only needs to be done once.
            WindowsUsbDeviceFactory.Register();
            WindowsHidDeviceFactory.Register();

            //DeviceConnectionExample.StartListening();
            DeviceConnectionExample.TrezorInitialized += _DeviceConnectionExample_TrezorInitialized;
            DeviceConnectionExample.TrezorDisconnected += _DeviceConnectionExample_TrezorDisconnected;

            await DeviceConnectionExample.InitializeTrezorAsync();
        }

        public void Dispose()
        {
            DeviceConnectionExample.Dispose();
        }
        public int Menu()
        {
            while (true)
            {
                //Console.Clear();
                Console.WriteLine();
                Console.WriteLine("1. Increment Connected Device");
                Console.WriteLine("2. Decrement Connected Device");
                Console.WriteLine("3. Save");
                var consoleKey = Console.ReadKey();
                if (consoleKey.KeyChar == '1') return 1;
                if (consoleKey.KeyChar == '2') return 2;
                if (consoleKey.KeyChar == '3') return 3;
            }
        }
        public async Task Go(int menuOption)
        {
            switch (menuOption)
            {
 
                case 1:
                    try
                    {
                        //await DeviceConnectionExample.InitializeTrezorAsync();
                        await IncrementAsync();
                        //DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                    //Console.ReadKey();
                    break;
                case 2:
                    try
                    {
                        //await DeviceConnectionExample.InitializeTrezorAsync();
                        await DecrementAsync();
                       // DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                   // Console.ReadKey();
                    break;
                case 3:
                    try
                    {
                       // await DeviceConnectionExample.InitializeTrezorAsync();
                        await SaveAsync();
                        //DeviceConnectionExample.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine(ex.ToString());
                    }
                    //Console.ReadKey();
                    break;
            }
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
            var bytes = await DeviceConnectionExample.WriteToDecrementAsync();
            Console.Clear();
            Console.WriteLine("Decrement Command Passed");
            Console.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
        private static async Task SaveAsync()
        {
            var bytes = await DeviceConnectionExample.WriteToSaveAsync();
            Console.Clear();
            Console.WriteLine("Increment Command Passed");
            Console.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
    }
}