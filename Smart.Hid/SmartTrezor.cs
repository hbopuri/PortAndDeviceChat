using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Device.Net;

namespace Smart.Hid
{
    public class SmartTrezor : IDisposable
    {
        #region Fields

        //Define the types of devices to search for. This particular device can be connected to via USB, or Hid
        private readonly List<FilterDeviceDefinition> _deviceDefinitions = new List<FilterDeviceDefinition>
        {
            new FilterDeviceDefinition
            {
                DeviceType = DeviceType.Hid, VendorId = 0x04D8, ProductId = 0x00DE, Label = "MCP2210 USB to SPI Master",
                UsagePage = 65280
            }
        };

        #endregion

        #region Events

        public event EventHandler TrezorInitialized;
        public event EventHandler TrezorDisconnected;

        #endregion

        #region Public Properties

        public IDevice TrezorDevice { get; private set; }
        public DeviceListener DeviceListener { get; private set; }

        #endregion

        #region Event Handlers

        private void DevicePoller_DeviceInitialized(object sender, DeviceEventArgs e)
        {
            TrezorDevice = e.Device;
            TrezorInitialized?.Invoke(this, new EventArgs());
        }

        private void DevicePoller_DeviceDisconnected(object sender, DeviceEventArgs e)
        {
            TrezorDevice = null;
            TrezorDisconnected?.Invoke(this, new EventArgs());
        }

        #endregion

        #region Public Methods

        public void StartListening()
        {
            TrezorDevice?.Dispose();
            DeviceListener = new DeviceListener(_deviceDefinitions, 3000);
            DeviceListener.DeviceDisconnected += DevicePoller_DeviceDisconnected;
            DeviceListener.DeviceInitialized += DevicePoller_DeviceInitialized;
        }

        public async Task InitializeTrezorAsync()
        {
            //Get the first available device and connect to it
            var devices = await DeviceManager.Current.GetDevicesAsync(_deviceDefinitions);
            TrezorDevice = devices.FirstOrDefault();
            if (TrezorDevice != null) await TrezorDevice.InitializeAsync();
        }

        public async Task<byte[]> WriteAndReadFromDeviceAsync()
        {
            //Create a buffer with 3 bytes (initialize)
            var writeBuffer = new byte[3];
            writeBuffer[0] = 0x3f;
            writeBuffer[1] = 0x23;
            writeBuffer[2] = 0x23;

            //Write the data to the device
            return await TrezorDevice.WriteAndReadAsync(writeBuffer);
        }

        public async Task<byte[]> WriteToIncrementAsync(byte[] writeBuffer)
        {
            //GPIO-1 Chip Select (should become low) Strain Gage
            //Create a buffer with 3 bytes (initialize)
           

            //Write the data to the device
            return await TrezorDevice.WriteAndReadAsync(writeBuffer);
        }

        public async Task<byte[]> WriteToDecrementAsync(byte[] writeBuffer)
        {
            //GPIO-1 Chip Select (should become low) Strain Gage
            //Create a buffer with 3 bytes (initialize)
           

            //Write the data to the device
            return await TrezorDevice.WriteAndReadAsync(writeBuffer);
        }
        public async Task<byte[]> WriteToSaveAsync(byte[] writeBuffer)
        {
            //GPIO-1 Chip Select (should become low) Strain Gage
            //Create a buffer with 3 bytes (initialize)
           

            //Write the data to the device
            return await TrezorDevice.WriteAndReadAsync(writeBuffer);
        }
        public void Dispose()
        {
            TrezorDevice?.Dispose();
        }

        #endregion
    }
}
