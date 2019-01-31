namespace Smart.Agent.Model
{
    public class UsbDeviceInfo
    {
        public UsbDeviceInfo(string deviceId, string pnpDeviceId, string description)
        {
            DeviceId = deviceId;
            PnpDeviceId = pnpDeviceId;
            Description = description;
        }
        public string DeviceId { get; private set; }
        public string PnpDeviceId { get; private set; }
        public string Description { get; private set; }
    }
}
