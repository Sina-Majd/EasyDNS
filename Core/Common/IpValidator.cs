using System;
using System.Net;
using System.Net.Sockets;

namespace EasyDNS.Core.Common
{
    public static class IpValidator
    {
        /// <summary>
        /// Validates whether a string is a valid IPv4 address (strictly 4 octets, 0-255, no leading zeros).
        /// </summary>
        public static bool IsValidIpv4(string? ipString)
        {
            if (string.IsNullOrWhiteSpace(ipString)) return false;
            string trimmed = ipString.Trim();
            string[] parts = trimmed.Split('.');
            if (parts.Length != 4) return false;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (!byte.TryParse(part, out _)) return false;
                if (part.Length > 1 && part.StartsWith("0", StringComparison.Ordinal)) return false;
            }

            return IPAddress.TryParse(trimmed, out var address) &&
                   address.AddressFamily == AddressFamily.InterNetwork;
        }

        /// <summary>
        /// Validates whether a string is a valid IPv6 address.
        /// </summary>
        public static bool IsValidIpv6(string? ipString)
        {
            if (string.IsNullOrWhiteSpace(ipString)) return false;
            string trimmed = ipString.Trim();
            if (!trimmed.Contains(':')) return false;

            return IPAddress.TryParse(trimmed, out var address) &&
                   address.AddressFamily == AddressFamily.InterNetworkV6;
        }

        /// <summary>
        /// Validates whether a string is either a valid IPv4 or IPv6 address.
        /// </summary>
        public static bool IsValidIp(string? ipString)
        {
            return IsValidIpv4(ipString) || IsValidIpv6(ipString);
        }
    }
}
