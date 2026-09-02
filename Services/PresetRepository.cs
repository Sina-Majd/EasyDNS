using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EasyDNS.Models;

namespace EasyDNS.Services
{
    public class PresetRepository
    {
        private readonly List<DnsPreset> _presets;
        private readonly string _customConfigPath;

        public PresetRepository()
        {
            _presets = new List<DnsPreset>();

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyDNS");
            if (!Directory.Exists(appData))
            {
                try { Directory.CreateDirectory(appData); } catch { }
            }
            _customConfigPath = Path.Combine(appData, "custom_presets.xml");

            LoadDefaultPresets();
            LoadCustomPresets();
        }

        private void LoadDefaultPresets()
        {
            _presets.Clear();

            // Universal / Global DNS Providers
            _presets.Add(new DnsPreset("Cloudflare (1.1.1.1)", "1.1.1.1", "1.0.0.1", "Universal", "Ultra-fast, privacy-first DNS by Cloudflare & APNIC."));
            _presets.Add(new DnsPreset("Google Public DNS", "8.8.8.8", "8.8.4.4", "Universal", "Global, highly reliable public DNS service by Google."));
            _presets.Add(new DnsPreset("Quad9 (Malware Blocking)", "9.9.9.9", "149.112.112.112", "Universal", "Blocks malicious domains, phishing, and botnets with zero logging."));
            _presets.Add(new DnsPreset("Quad9 (Uncensored)", "9.9.9.10", "149.112.112.10", "Universal", "Uncensored, non-blocking Quad9 DNS without filtering."));
            _presets.Add(new DnsPreset("OpenDNS Home", "208.67.222.222", "208.67.220.220", "Universal", "Cisco Umbrella cloud DNS with intelligent routing and protection."));
            
            // Ad-Blocking & Privacy (Universal)
            _presets.Add(new DnsPreset("AdGuard DNS (Default)", "94.140.14.14", "94.140.15.15", "Universal", "Blocks ads, trackers, banners, and phishing domains."));
            _presets.Add(new DnsPreset("AdGuard DNS (Family)", "94.140.14.15", "94.140.15.16", "Universal", "Blocks ads, trackers, adult websites and enables safe search."));
            _presets.Add(new DnsPreset("Control D (Malware)", "76.76.2.1", "76.76.10.1", "Universal", "Blocks malware, phishing, and known malicious hosts."));
            _presets.Add(new DnsPreset("Control D (Unfiltered)", "76.76.2.0", "76.76.10.0", "Universal", "High-performance unfiltered DNS by Control D."));
            _presets.Add(new DnsPreset("NextDNS", "45.90.28.0", "45.90.30.0", "Universal", "Modern cloud DNS with advanced privacy protection."));

            // Content Protection & Tier 1 (Universal)
            _presets.Add(new DnsPreset("Cloudflare (Malware Block)", "1.1.1.2", "1.0.0.2", "Universal", "1.1.1.1 with automated malware and threat blocking."));
            _presets.Add(new DnsPreset("Cloudflare (Family Safe)", "1.1.1.3", "1.0.0.3", "Universal", "Blocks malware and adult content automatically."));
            _presets.Add(new DnsPreset("OpenDNS FamilyShield", "208.67.222.123", "208.67.220.123", "Universal", "Pre-configured adult content blocking by OpenDNS."));
            _presets.Add(new DnsPreset("CleanBrowsing (Security)", "185.228.168.9", "185.228.169.9", "Universal", "Blocks malware, phishing, and malicious domains."));
            _presets.Add(new DnsPreset("CleanBrowsing (Family)", "185.228.168.168", "185.228.169.168", "Universal", "Blocks adult content, phishing, and enforces SafeSearch."));
            _presets.Add(new DnsPreset("Comodo Secure DNS", "8.26.56.26", "8.20.247.20", "Universal", "Cloud-based security DNS by Comodo CyberSecurity."));
            _presets.Add(new DnsPreset("DNS.WATCH", "84.200.69.80", "84.200.70.40", "Universal", "Fast, uncensored, no-logging European DNS."));
            _presets.Add(new DnsPreset("Level3 (CenturyLink)", "4.2.2.1", "4.2.2.2", "Universal", "Tier-1 backbone DNS server infrastructure."));

            // Persian Services (Anti-Sanction & Gaming)
            _presets.Add(new DnsPreset("Shecan (Dev & Bypass)", "178.22.122.100", "185.51.200.2", "Persian", "Bypasses international developer and software sanctions."));
            _presets.Add(new DnsPreset("Electro DNS (Gaming)", "78.157.42.101", "78.157.42.100", "Persian", "Optimized routing for gaming and regional service access."));
            _presets.Add(new DnsPreset("403.online", "10.202.10.202", "10.202.10.102", "Persian", "Developer and service unblocker DNS for IT professionals."));
            _presets.Add(new DnsPreset("Radar Game", "10.202.10.10", "10.202.10.11", "Persian", "Low-ping gaming DNS for reduced latency and packet loss."));
        }

        public List<DnsPreset> GetAllPresets()
        {
            return _presets.ToList();
        }

        public List<string> GetCategories()
        {
            var categories = new List<string>();
            categories.Add("All");
            categories.Add("Universal");
            categories.Add("Persian");
            return categories;
        }

        public void AddCustomPreset(string name, string primaryDns, string secondaryDns, string description)
        {
            var preset = new DnsPreset(name, primaryDns, secondaryDns, "Universal", description);
            preset.IsCustom = true;
            _presets.Add(preset);
            SaveCustomPresets();
        }

        public void DeleteCustomPreset(string presetId)
        {
            var item = _presets.FirstOrDefault(delegate(DnsPreset p) { return p.Id == presetId && p.IsCustom; });
            if (item != null)
            {
                _presets.Remove(item);
                SaveCustomPresets();
            }
        }

        private void SaveCustomPresets()
        {
            try
            {
                var customList = _presets.Where(delegate(DnsPreset p) { return p.IsCustom; }).ToList();
                var doc = new XDocument(
                    new XElement("CustomPresets",
                        customList.Select(delegate(DnsPreset p)
                        {
                            return new XElement("Preset",
                                new XAttribute("Id", p.Id ?? ""),
                                new XElement("Name", p.Name ?? ""),
                                new XElement("PrimaryDns", p.PrimaryDns ?? ""),
                                new XElement("SecondaryDns", p.SecondaryDns ?? ""),
                                new XElement("Description", p.Description ?? "")
                            );
                        })
                    )
                );
                doc.Save(_customConfigPath);
            }
            catch { }
        }

        private void LoadCustomPresets()
        {
            try
            {
                if (!File.Exists(_customConfigPath)) return;
                var doc = XDocument.Load(_customConfigPath);
                if (doc.Root == null) return;

                var elements = doc.Root.Elements("Preset");
                if (elements == null) return;

                foreach (var el in elements)
                {
                    string id = el.Attribute("Id") != null ? el.Attribute("Id").Value : Guid.NewGuid().ToString("N");
                    string name = el.Element("Name") != null ? el.Element("Name").Value : "Custom DNS";
                    string primary = el.Element("PrimaryDns") != null ? el.Element("PrimaryDns").Value : "";
                    string secondary = el.Element("SecondaryDns") != null ? el.Element("SecondaryDns").Value : "";
                    string desc = el.Element("Description") != null ? el.Element("Description").Value : "User-defined custom DNS";

                    if (!string.IsNullOrEmpty(primary))
                    {
                        var preset = new DnsPreset(name, primary, secondary, "Universal", desc);
                        preset.Id = id;
                        preset.IsCustom = true;
                        _presets.Add(preset);
                    }
                }
            }
            catch { }
        }
    }
}
