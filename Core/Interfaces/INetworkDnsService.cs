using System.Collections.Generic;
using System.Threading.Tasks;
using EasyDNS.Core.Common;
using EasyDNS.Core.Models;

namespace EasyDNS.Core.Interfaces
{
    public interface INetworkDnsService
    {
        bool IsRunAsAdmin();
        IReadOnlyList<NetworkAdapterInfo> GetNetworkAdapters();
        Task<OperationResult> SetDnsAsync(NetworkAdapterInfo adapter, string primaryDns, string? secondaryDns, string? dohTemplate = null, string? primaryIpv6 = null, string? secondaryIpv6 = null);
        Task<OperationResult> ResetToDhcpAsync(NetworkAdapterInfo adapter);
        Task<OperationResult> FlushDnsCacheAsync();
    }
}
