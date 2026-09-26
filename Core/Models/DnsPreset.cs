using System;

namespace EasyDNS.Core.Models
{
    public class DnsPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string PrimaryDns { get; set; } = string.Empty;
        public string SecondaryDns { get; set; } = string.Empty;
        public string? PrimaryIpv6 { get; set; }
        public string? SecondaryIpv6 { get; set; }
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public bool IsCustom { get; set; }
        public string DohTemplate { get; set; } = string.Empty;

        public DnsPreset()
        {
        }

        public DnsPreset(
            string name,
            string primaryDns,
            string secondaryDns,
            string category,
            string description,
            string dohTemplate = "",
            string? primaryIpv6 = null,
            string? secondaryIpv6 = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            PrimaryDns = primaryDns;
            SecondaryDns = secondaryDns;
            Category = category;
            Description = description;
            DohTemplate = dohTemplate;
            PrimaryIpv6 = primaryIpv6;
            SecondaryIpv6 = secondaryIpv6;
        }

        public override string ToString()
        {
            string secondary = string.IsNullOrEmpty(SecondaryDns) ? "" : $", {SecondaryDns}";
            return $"{Name} ({PrimaryDns}{secondary})";
        }
    }
}
