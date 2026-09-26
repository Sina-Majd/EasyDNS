namespace EasyDNS.Core.Models
{
    public class LatencyResult
    {
        public string HostOrIp { get; }
        public long RoundtripTimeMs { get; }
        public bool Success { get; }
        public string StatusMessage { get; }
        public string Protocol { get; }

        public LatencyResult(string hostOrIp, long rttMs, bool success, string statusMessage = "", string protocol = "ICMP")
        {
            HostOrIp = hostOrIp;
            RoundtripTimeMs = rttMs;
            Success = success;
            StatusMessage = statusMessage;
            Protocol = protocol;
        }

        public string DisplayText => Success ? $"{RoundtripTimeMs} ms" : "Timeout";
    }
}
