using System.Management;

namespace Smart.Agent.Helper
{
    public class ProcessConnection
    {
        public static ConnectionOptions ProcessConnectionOptions()
        {
            ConnectionOptions options = new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.Default,
                EnablePrivileges = true
            };
            return options;
        }

        public static ManagementScope ConnectionScope(string machineName, ConnectionOptions options, string path)
        {
            ManagementScope connectScope = new ManagementScope
            {
                Path = new ManagementPath(@"\\" + machineName + path), Options = options
            };
            connectScope.Connect();
            return connectScope;
        }
    }
}