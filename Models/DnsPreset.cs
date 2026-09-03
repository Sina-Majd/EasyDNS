using System;

namespace EasyDNS.Models
{
    public class DnsPreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PrimaryDns { get; set; }
        public string SecondaryDns { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Tag { get; set; }
        public bool IsCustom { get; set; }
        public long? LatencyMs { get; set; }
        public bool IsCheckingLatency { get; set; }
        public bool IsActive { get; set; }
        public string DohTemplate { get; set; }

        public DnsPreset()
        {
            Id = Guid.NewGuid().ToString("N");
            DohTemplate = "";
        }

        public DnsPreset(string name, string primaryDns, string secondaryDns, string category, string description, string dohTemplate = "")
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            PrimaryDns = primaryDns;
            SecondaryDns = secondaryDns;
            Category = category;
            Description = description;
            Tag = "";
            IsCustom = false;
            IsActive = false;
            DohTemplate = dohTemplate ?? "";
        }

        public override string ToString()
        {
            return Name + " (" + PrimaryDns + (string.IsNullOrEmpty(SecondaryDns) ? "" : ", " + SecondaryDns) + ")";
        }
    }
}
