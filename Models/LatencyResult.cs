namespace EasyDNS.Models
{
    public class LatencyResult
    {
        public string HostOrIp { get; set; }
        public long RoundtripTimeMs { get; set; }
        public bool Success { get; set; }
        public string StatusMessage { get; set; }
        public string Protocol { get; set; }

        public LatencyResult(string hostOrIp, long rttMs, bool success, string statusMessage = "", string protocol = "ICMP")
        {
            HostOrIp = hostOrIp;
            RoundtripTimeMs = rttMs;
            Success = success;
            StatusMessage = statusMessage;
            Protocol = protocol ?? "ICMP";
        }

        public string DisplayText
        {
            get
            {
                if (!Success) return "Timeout";
                return RoundtripTimeMs + " ms";
            }
        }
    }
}
