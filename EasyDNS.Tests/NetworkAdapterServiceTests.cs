using System;
using System.Diagnostics;
using EasyDNS.Infrastructure.Services;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace EasyDNS.Tests
{
    public class NetworkAdapterServiceTests
    {
        private readonly ITestOutputHelper _output;

        public NetworkAdapterServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GetNetworkAdapters_ShouldReturnControlPanelAdapters()
        {
            var service = new WindowsIpHelperDnsService();
            var adapters = service.GetNetworkAdapters();

            adapters.Should().NotBeNull();
            _output.WriteLine($"Total adapters found: {adapters.Count}");
            foreach (var a in adapters)
            {
                _output.WriteLine($"Adapter: Name='{a.Name}', Desc='{a.Description}', Up={a.IsOperational}, Type={a.TypeName}, AutomaticDns={a.IsDnsAutomatic}, DNS=[{string.Join(", ", a.DnsServers)}]");
            }

            // On Windows 10/11 with active networking, adapters should be returned
            adapters.Count.Should().BeGreaterThan(0);
            adapters.Count.Should().BeLessThanOrEqualTo(10); // Control panel adapters, not 15+ miniports
        }

        [Fact]
        public void NetworkAdapterInfo_IsDnsAutomatic_DefaultsToTrue()
        {
            var info = new Core.Models.NetworkAdapterInfo();
            info.IsDnsAutomatic.Should().BeTrue();
        }
    }
}
