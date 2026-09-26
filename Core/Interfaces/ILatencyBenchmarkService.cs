using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyDNS.Core.Models;

namespace EasyDNS.Core.Interfaces
{
    public interface ILatencyBenchmarkService
    {
        Task<LatencyResult> MeasureLatencyAsync(string ipOrHost, int timeoutMs = 1200, CancellationToken ct = default);
        Task<LatencyResult> MeasurePresetLatencyAsync(DnsPreset preset, int timeoutMs = 1200, CancellationToken ct = default);
        Task BenchmarkAllAsync(
            IEnumerable<DnsPreset> presets,
            Action<DnsPreset, LatencyResult>? onPresetTested = null,
            CancellationToken ct = default,
            int maxConcurrency = 8);
    }
}
