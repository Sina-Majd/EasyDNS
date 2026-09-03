using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EasyDNS.Models;

namespace EasyDNS.Services
{
    public class LatencyBenchmarkService
    {
        private const int DefaultTimeoutMs = 1200;
        private const int DefaultDnsUdpTimeoutMs = 750;
        private const int DefaultMaxConcurrency = 5;

        private static readonly byte[] DnsQueryPayload = new byte[]
        {
            0xAA, 0xBB, // Transaction ID
            0x01, 0x00, // Standard query, recursion desired
            0x00, 0x01, // QDCOUNT = 1
            0x00, 0x00, // ANCOUNT = 0
            0x00, 0x00, // NSCOUNT = 0
            0x00, 0x00, // ARCOUNT = 0
            // QNAME: 6 google 3 com 0
            0x06, 0x67, 0x6f, 0x6f, 0x67, 0x6c, 0x65,
            0x03, 0x63, 0x6f, 0x6d, 0x00,
            0x00, 0x01, // QTYPE = A (IPv4)
            0x00, 0x01  // QCLASS = IN
        };

        /// <summary>
        /// Attempts to measure DNS query resolution latency via UDP port 53.
        /// Returns null if query times out or fails (e.g., port 53 is blocked).
        /// </summary>
        private async Task<LatencyResult> MeasureDnsQueryLatencyAsync(string ipOrHost, int timeoutMs, CancellationToken ct)
        {
            IPAddress ip;
            if (!IPAddress.TryParse(ipOrHost.Trim(), out ip))
            {
                return null;
            }

            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.Client.SendTimeout = timeoutMs;
                    udpClient.Client.ReceiveTimeout = timeoutMs;

                    var endPoint = new IPEndPoint(ip, 53);
                    udpClient.Connect(endPoint);

                    // Generate a unique 16-bit transaction ID
                    byte[] query = (byte[])DnsQueryPayload.Clone();
                    ushort txId = (ushort)new Random().Next(1, 65535);
                    query[0] = (byte)(txId >> 8);
                    query[1] = (byte)(txId & 0xFF);

                    var sw = Stopwatch.StartNew();
                    await udpClient.SendAsync(query, query.Length);

                    var receiveTask = udpClient.ReceiveAsync();
                    var timeoutTask = Task.Delay(timeoutMs, ct);

                    var completed = await Task.WhenAny(receiveTask, timeoutTask);
                    sw.Stop();

                    if (completed == receiveTask && !receiveTask.IsFaulted)
                    {
                        var result = receiveTask.Result;
                        if (result.Buffer != null && result.Buffer.Length >= 12)
                        {
                            if (result.Buffer[0] == query[0] && result.Buffer[1] == query[1])
                            {
                                long rtt = Math.Max(1, sw.ElapsedMilliseconds);
                                return new LatencyResult(ipOrHost, rtt, true, "OK", "DNS");
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Measures latency for an IP address using hybrid testing:
        /// Tries a real UDP DNS query (port 53) first; if blocked or timed out, falls back to ICMP Ping.
        /// </summary>
        public async Task<LatencyResult> MeasureLatencyAsync(string ipOrHost, int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(ipOrHost))
            {
                return new LatencyResult(ipOrHost, -1, false, "Invalid IP", "None");
            }

            if (ct.IsCancellationRequested)
            {
                return new LatencyResult(ipOrHost, -1, false, "Cancelled", "None");
            }

            // 1. Attempt True DNS query first
            var dnsResult = await MeasureDnsQueryLatencyAsync(ipOrHost, Math.Min(timeoutMs, DefaultDnsUdpTimeoutMs), ct);
            if (dnsResult != null && dnsResult.Success)
            {
                return dnsResult;
            }

            if (ct.IsCancellationRequested)
            {
                return new LatencyResult(ipOrHost, -1, false, "Cancelled", "None");
            }

            // 2. Fallback to ICMP Ping
            try
            {
                using (var pinger = new Ping())
                {
                    var reply = await pinger.SendPingAsync(ipOrHost.Trim(), timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        return new LatencyResult(ipOrHost, reply.RoundtripTime, true, "OK", "ICMP");
                    }
                    else
                    {
                        return new LatencyResult(ipOrHost, -1, false, reply.Status.ToString(), "ICMP");
                    }
                }
            }
            catch (Exception ex)
            {
                return new LatencyResult(ipOrHost, -1, false, ex.Message, "ICMP");
            }
        }

        /// <summary>
        /// Measures latency for a DNS preset (checks primary DNS, or secondary if primary fails).
        /// </summary>
        public async Task<LatencyResult> MeasurePresetLatencyAsync(DnsPreset preset, int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default(CancellationToken))
        {
            if (preset == null) return new LatencyResult("", -1, false, "Null preset", "None");

            preset.IsCheckingLatency = true;
            try
            {
                var result = await MeasureLatencyAsync(preset.PrimaryDns, timeoutMs, ct);
                if (!result.Success && !string.IsNullOrWhiteSpace(preset.SecondaryDns) && !ct.IsCancellationRequested)
                {
                    var secResult = await MeasureLatencyAsync(preset.SecondaryDns, timeoutMs, ct);
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
        /// Benchmarks all presets with concurrency throttling and cancellation support.
        /// </summary>
        public async Task BenchmarkAllAsync(IEnumerable<DnsPreset> presets, Action<DnsPreset, LatencyResult> onPresetTested = null, CancellationToken ct = default(CancellationToken), int maxConcurrency = DefaultMaxConcurrency)
        {
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new List<Task>();

            foreach (var preset in presets)
            {
                if (ct.IsCancellationRequested) break;

                var targetPreset = preset;
                tasks.Add(Task.Run(async delegate
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        if (ct.IsCancellationRequested) return;

                        var result = await MeasurePresetLatencyAsync(targetPreset, DefaultTimeoutMs, ct);
                        if (onPresetTested != null && !ct.IsCancellationRequested)
                        {
                            onPresetTested(targetPreset, result);
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }

            try
            {
                await Task.WhenAll(tasks.ToArray());
            }
            catch (OperationCanceledException) { }
        }
    }
}
