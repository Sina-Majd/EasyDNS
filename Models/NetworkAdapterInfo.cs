using System.Collections.Generic;

namespace EasyDNS.Models
{
    public class NetworkAdapterInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypeName { get; set; }
        public bool IsDhcpEnabled { get; set; }
        public List<string> DnsServers { get; set; }
        public bool IsOperational { get; set; }
        public string MacAddress { get; set; }
        public string IpAddress { get; set; }

        public NetworkAdapterInfo()
        {
            DnsServers = new List<string>();
        }

        public string PrimaryDns
        {
            get
            {
                return (DnsServers != null && DnsServers.Count > 0) ? DnsServers[0] : string.Empty;
            }
        }

        public string SecondaryDns
        {
            get
            {
                return (DnsServers != null && DnsServers.Count > 1) ? DnsServers[1] : string.Empty;
            }
        }

        public string DisplayName
        {
            get
            {
                return Name + " (" + Description + ")";
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
