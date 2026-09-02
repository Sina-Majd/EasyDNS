using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using EasyDNS.Models;

namespace EasyDNS.Services
{
    public class LatencyBenchmarkService
    {
        private const int DefaultTimeoutMs = 1200;

        /// <summary>
        /// Pings an IP address asynchronously and returns the roundtrip latency.
        /// </summary>
        public async Task<LatencyResult> MeasureLatencyAsync(string ipOrHost, int timeoutMs = DefaultTimeoutMs)
        {
            if (string.IsNullOrWhiteSpace(ipOrHost))
            {
                return new LatencyResult(ipOrHost, -1, false, "Invalid IP");
            }

            try
            {
                using (var pinger = new Ping())
                {
                    var reply = await pinger.SendPingAsync(ipOrHost.Trim(), timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        return new LatencyResult(ipOrHost, reply.RoundtripTime, true, "OK");
                    }
                    else
                    {
                        return new LatencyResult(ipOrHost, -1, false, reply.Status.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                return new LatencyResult(ipOrHost, -1, false, ex.Message);
            }
        }

        /// <summary>
        /// Measures latency for a DNS preset (checks primary DNS, or secondary if primary fails).
        /// </summary>
        public async Task<LatencyResult> MeasurePresetLatencyAsync(DnsPreset preset, int timeoutMs = DefaultTimeoutMs)
        {
            if (preset == null) return new LatencyResult("", -1, false, "Null preset");

            preset.IsCheckingLatency = true;
            try
            {
                var result = await MeasureLatencyAsync(preset.PrimaryDns, timeoutMs);
                if (!result.Success && !string.IsNullOrWhiteSpace(preset.SecondaryDns))
                {
                    var secResult = await MeasureLatencyAsync(preset.SecondaryDns, timeoutMs);
                    if (secResult.Success)
                    {
                        result = secResult;
                    }
                }

                preset.LatencyMs = result.Success ? (long?)result.RoundtripTimeMs : null;
                return result;
            }
            finally
            {
                preset.IsCheckingLatency = false;
            }
        }

        /// <summary>
        /// Benchmarks all presets in parallel and fires progress callbacks.
        /// </summary>
        public async Task BenchmarkAllAsync(IEnumerable<DnsPreset> presets, Action<DnsPreset, LatencyResult> onPresetTested = null)
        {
            var tasks = new List<Task>();

            foreach (var preset in presets)
            {
                var targetPreset = preset;
                tasks.Add(Task.Run(async () =>
                {
                    var result = await MeasurePresetLatencyAsync(targetPreset);
                    if (onPresetTested != null)
                    {
                        onPresetTested(targetPreset, result);
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
    }
}
