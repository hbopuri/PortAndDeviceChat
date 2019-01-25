using System;
using System.Collections.Generic;
using System.Management;

namespace Smart.Agent.Helper
{
    public class ComPortInfo
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public static List<ComPortInfo> GetComPortsInfo()
        {
            List<ComPortInfo> comPortInfoList = new List<ComPortInfo>();
            ConnectionOptions options = ProcessConnection.ProcessConnectionOptions();
            ManagementScope connectionScope =
                ProcessConnection.ConnectionScope(Environment.MachineName, options, @"\root\CIMV2");
            ObjectQuery objectQuery = new ObjectQuery("SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0");
            ManagementObjectSearcher comPortSearcher = new ManagementObjectSearcher(connectionScope, objectQuery);
            using (comPortSearcher)
            {
                foreach (var o in comPortSearcher.Get())
                {
                    var obj = (ManagementObject) o;
                    var captionObj = obj?["Caption"];
                    if (captionObj == null) continue;
                    var caption = captionObj.ToString();
                    if (!caption.Contains("(COM")) continue;
                    ComPortInfo comPortInfo = new ComPortInfo
                    {
                        Name = caption.Substring(caption.LastIndexOf("(COM", StringComparison.Ordinal))
                            .Replace("(", string.Empty).Replace(")",
                                string.Empty),
                        Description = caption
                    };
                    comPortInfoList.Add(comPortInfo);
                }
            }

            return comPortInfoList;
        }
    }
}