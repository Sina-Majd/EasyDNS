using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyDNS.Core.Models;
using EasyDNS.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace EasyDNS.Tests
{
    public class LatencyBenchmarkTests
    {
        [Fact]
        public async Task MeasureLatencyAsync_InvalidIp_ShouldFail()
        {
            var service = new LatencyBenchmarkService();
            var result = await service.MeasureLatencyAsync("not.an.ip");

            result.Success.Should().BeFalse();
            result.StatusMessage.Should().Be("Invalid IP");
        }

        [Fact]
        public async Task BenchmarkAllAsync_ShouldRespectCancellation()
        {
            var service = new LatencyBenchmarkService();
            var presets = new List<DnsPreset>
            {
                new("DNS1", "1.1.1.1", "1.0.0.1", "General", "Desc"),
                new("DNS2", "8.8.8.8", "8.8.4.4", "General", "Desc"),
                new("DNS3", "9.9.9.9", "149.112.112.112", "Security", "Desc"),
                new("DNS4", "208.67.222.222", "208.67.220.220", "General", "Desc")
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            int testedCount = 0;
            await service.BenchmarkAllAsync(presets, (p, r) =>
            {
                Interlocked.Increment(ref testedCount);
            }, cts.Token);

            testedCount.Should().Be(0);
        }
    }
}
