using System;
using System.Threading.Tasks;
using Hid.Net.Windows;
using Smart.Log;
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
            SmartLog.WriteLine(string.Join(" ", readBuffer));
            Console.ReadKey();
        }
        private static async Task DisplayDataAsync()
        {
            var bytes = await DeviceConnectionExample.WriteAndReadFromDeviceAsync();
            Console.Clear();
            SmartLog.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }

        private static void DisplayWaitMessage()
        {
            SmartLog.WriteLine("Waiting for device to be plugged in...");
        }

        private static void _DeviceConnectionExample_TrezorDisconnected(object sender, EventArgs e)
        {
            Console.Clear();
            SmartLog.WriteLine("Disconnected.");
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
                SmartLog.WriteLine(ex.ToString());
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
                SmartLog.WriteLine();
                SmartLog.WriteLine("1. Increment Connected Device");
                SmartLog.WriteLine("2. Decrement Connected Device");
                SmartLog.WriteLine("3. Save");
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
                        SmartLog.WriteLine(ex.ToString());
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
                        SmartLog.WriteLine(ex.ToString());
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
                        SmartLog.WriteLine(ex.ToString());
                    }
                    //Console.ReadKey();
                    break;
            }
        }
        private static async Task IncrementAsync()
        {
            var bytes = await DeviceConnectionExample.WriteToIncrementAsync();
            Console.Clear();
            SmartLog.WriteLine("Increment Command Passed");
            SmartLog.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
        private static async Task DecrementAsync()
        {
            var bytes = await DeviceConnectionExample.WriteToDecrementAsync();
            Console.Clear();
            SmartLog.WriteLine("Decrement Command Passed");
            SmartLog.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
        private static async Task SaveAsync()
        {
            var bytes = await DeviceConnectionExample.WriteToSaveAsync();
            Console.Clear();
            SmartLog.WriteLine("Increment Command Passed");
            SmartLog.WriteLine("Device connected. Output:");
            DisplayData(bytes);
        }
    }
}