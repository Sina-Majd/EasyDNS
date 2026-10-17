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

        [Fact]
        public async Task MeasureLatencyAsync_WithCancelledToken_ReturnsCancelledResultImmediately()
        {
            var service = new LatencyBenchmarkService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await service.MeasureLatencyAsync("8.8.8.8", timeoutMs: 3000, ct: cts.Token);
            sw.Stop();

            result.Success.Should().BeFalse();
            result.StatusMessage.Should().Be("Cancelled");
            sw.ElapsedMilliseconds.Should().BeLessThan(500); // Should return immediately, not wait for timeout
        }
    }
}
