using System.Collections.Generic;
using System.Management;
using Smart.Agent.Model;

namespace Smart.Agent.Helper
{
    public class UsbDevice
    {
        public static List<UsbDeviceInfo> GetUsbDevices()
        {
            List<UsbDeviceInfo> devices = new List<UsbDeviceInfo>();

            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = searcher.Get();

            foreach (var device in collection)
            {
                devices.Add(new UsbDeviceInfo(
                    (string) device.GetPropertyValue("DeviceID"),
                    (string) device.GetPropertyValue("PNPDeviceID"),
                    (string) device.GetPropertyValue("Description")
                ));
            }

            collection.Dispose();
            return devices;
        }
    }
}