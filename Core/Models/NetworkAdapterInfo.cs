using System;
using System.Collections.Generic;

namespace EasyDNS.Core.Models
{
    public class NetworkAdapterInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsDhcpEnabled { get; set; }
        public bool IsDnsAutomatic { get; set; } = true;
        public List<string> DnsServers { get; set; } = new();
        public List<string> DnsServersIpv6 { get; set; } = new();
        public bool IsOperational { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;

        public string PrimaryDns => DnsServers.Count > 0 ? DnsServers[0] : string.Empty;
        public string SecondaryDns => DnsServers.Count > 1 ? DnsServers[1] : string.Empty;

        public string PrimaryDnsIpv6 => DnsServersIpv6.Count > 0 ? DnsServersIpv6[0] : string.Empty;
        public string SecondaryDnsIpv6 => DnsServersIpv6.Count > 1 ? DnsServersIpv6[1] : string.Empty;

        public string DisplayName => $"{Name} ({Description})";

        public override string ToString() => DisplayName;
    }
}
