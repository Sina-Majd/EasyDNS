using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;

namespace EasyDNS.Infrastructure.Services
{
    public class LatencyBenchmarkService : ILatencyBenchmarkService
    {
        private const int DefaultTimeoutMs = 1200;
        private const int DefaultDnsUdpTimeoutMs = 750;
        private const int DefaultMaxConcurrency = 8;

        private static readonly byte[] DnsQueryPayload = new byte[]
        {
            0xAA, 0xBB, // Transaction ID placeholder
            0x01, 0x00, // Standard query with recursion desired
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

        private static async Task<LatencyResult?> MeasureDnsQueryLatencyAsync(string ipOrHost, int timeoutMs, CancellationToken ct)
        {
            if (!IPAddress.TryParse(ipOrHost.Trim(), out var ip))
            {
                return null;
            }

            try
            {
                using var udpClient = new UdpClient(ip.AddressFamily);
                udpClient.Client.SendTimeout = timeoutMs;
                udpClient.Client.ReceiveTimeout = timeoutMs;

                var endPoint = new IPEndPoint(ip, 53);
                udpClient.Connect(endPoint);

                byte[] query = (byte[])DnsQueryPayload.Clone();
                ushort txId = (ushort)Random.Shared.Next(1, 65535);
                query[0] = (byte)(txId >> 8);
                query[1] = (byte)(txId & 0xFF);

                var sw = Stopwatch.StartNew();
                await udpClient.SendAsync(query.AsMemory(), ct);

                var receiveTask = udpClient.ReceiveAsync(ct).AsTask();
                var timeoutTask = Task.Delay(timeoutMs, ct);

                var completed = await Task.WhenAny(receiveTask, timeoutTask);
                sw.Stop();

                if (completed == receiveTask && !receiveTask.IsFaulted && !receiveTask.IsCanceled)
                {
                    var result = receiveTask.Result;
                    if (result.Buffer.Length >= 12 && result.Buffer[0] == query[0] && result.Buffer[1] == query[1])
                    {
                        long rtt = Math.Max(1, sw.ElapsedMilliseconds);
                        return new LatencyResult(ipOrHost, rtt, true, "OK", "DNS");
                    }
                }
            }
            catch
            {
                // Silently fall through to ICMP
            }

            return null;
        }

        public async Task<LatencyResult> MeasureLatencyAsync(string ipOrHost, int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ipOrHost) || !IPAddress.TryParse(ipOrHost.Trim(), out _))
            {
                return new LatencyResult(ipOrHost, -1, false, "Invalid IP", "None");
            }

            if (ct.IsCancellationRequested)
            {
                return new LatencyResult(ipOrHost, -1, false, "Cancelled", "None");
            }

            // 1. Attempt True DNS query first via UDP 53
            var dnsResult = await MeasureDnsQueryLatencyAsync(ipOrHost, Math.Min(timeoutMs, DefaultDnsUdpTimeoutMs), ct);
            if (dnsResult is { Success: true })
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
                using var pinger = new Ping();
                var reply = await pinger.SendPingAsync(ipOrHost.Trim(), timeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    return new LatencyResult(ipOrHost, reply.RoundtripTime, true, "OK", "ICMP");
                }

                return new LatencyResult(ipOrHost, -1, false, reply.Status.ToString(), "ICMP");
            }
            catch (Exception ex)
            {
                return new LatencyResult(ipOrHost, -1, false, ex.Message, "ICMP");
            }
        }

        public async Task<LatencyResult> MeasurePresetLatencyAsync(DnsPreset preset, int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default)
        {
            if (preset == null) return new LatencyResult("", -1, false, "Null preset", "None");

            var result = await MeasureLatencyAsync(preset.PrimaryDns, timeoutMs, ct);
            if (!result.Success && !string.IsNullOrWhiteSpace(preset.SecondaryDns) && !ct.IsCancellationRequested)
            {
                var secResult = await MeasureLatencyAsync(preset.SecondaryDns, timeoutMs, ct);
                if (secResult.Success)
                {
                    result = secResult;
                }
            }

            return result;
        }

        public async Task BenchmarkAllAsync(
            IEnumerable<DnsPreset> presets,
            Action<DnsPreset, LatencyResult>? onPresetTested = null,
            CancellationToken ct = default,
            int maxConcurrency = DefaultMaxConcurrency)
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = ct
            };

            try
            {
                await Parallel.ForEachAsync(presets, options, async (preset, token) =>
                {
                    if (token.IsCancellationRequested) return;

                    var result = await MeasurePresetLatencyAsync(preset, DefaultTimeoutMs, token);
                    if (!token.IsCancellationRequested)
                    {
                        onPresetTested?.Invoke(preset, result);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Graceful benchmark cancellation
            }
        }
    }
}
