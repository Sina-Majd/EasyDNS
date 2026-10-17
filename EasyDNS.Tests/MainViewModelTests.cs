using System.Linq;
using EasyDNS.Core.Common;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;
using EasyDNS.Infrastructure.Services;
using EasyDNS.ViewModels;
using FluentAssertions;
using Xunit;

namespace EasyDNS.Tests
{
    public class MainViewModelTests
    {
        [Fact]
        public void ApplyPresetByName_WhenSearchFiltered_StillAppliesPresetCorrectly()
        {
            var logger = new AppLogger();
            var repo = new JsonPresetRepository();
            var dnsService = new WindowsIpHelperDnsService();
            var benchmarkService = new LatencyBenchmarkService();

            var vm = new MainViewModel(dnsService, benchmarkService, repo, logger);

            // Filter presets so that Google is not in FilteredPresets
            vm.SearchQuery = "Cloudflare";
            vm.FilteredPresets.Should().NotContain(p => p.Name == "Google Public DNS");

            // Apply Google via tray helper method
            vm.ApplyPresetByName("Google Public DNS");

            // Check that Google's IP was loaded into inputs
            vm.InputPrimaryDns.Should().Be("8.8.8.8");
            vm.InputSecondaryDns.Should().Be("8.8.4.4");
        }
    }
}
