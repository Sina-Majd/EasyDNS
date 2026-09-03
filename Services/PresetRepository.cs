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

            // 1. General DNS Resolvers (Fast & Public)
            _presets.Add(new DnsPreset("Cloudflare (1.1.1.1)", "1.1.1.1", "1.0.0.1", "General", "Ultra-fast, privacy-first DNS by Cloudflare & APNIC.", "https://cloudflare-dns.com/dns-query"));
            _presets.Add(new DnsPreset("Google Public DNS", "8.8.8.8", "8.8.4.4", "General", "Global, highly reliable public DNS service by Google.", "https://dns.google/dns-query"));
            _presets.Add(new DnsPreset("OpenDNS Home", "208.67.222.222", "208.67.220.220", "General", "Cisco Umbrella cloud DNS with intelligent routing and protection.", "https://doh.opendns.com/dns-query"));
            _presets.Add(new DnsPreset("Shecan", "178.22.122.100", "185.51.200.2", "General, Gaming", "Bypasses international restrictions and developer sanctions."));
            _presets.Add(new DnsPreset("DNS.WATCH", "84.200.69.80", "84.200.70.40", "General", "Fast, uncensored, no-logging European DNS."));
            _presets.Add(new DnsPreset("Level3 (CenturyLink)", "4.2.2.1", "4.2.2.2", "General", "Tier-1 backbone DNS server infrastructure."));

            // 2. Security & Privacy Resolvers
            _presets.Add(new DnsPreset("AdGuard DNS (Default)", "94.140.14.14", "94.140.15.15", "Security", "Blocks ads, trackers, banners, and phishing domains.", "https://dns.adguard-dns.com/dns-query"));
            _presets.Add(new DnsPreset("AdGuard DNS (Family)", "94.140.14.15", "94.140.15.16", "Security", "Blocks ads, trackers, adult websites and enables safe search.", "https://dns.adguard-dns.com/dns-query"));
            _presets.Add(new DnsPreset("Quad9 (Malware Blocking)", "9.9.9.9", "149.112.112.112", "Security", "Blocks malicious domains, phishing, and botnets with zero logging.", "https://dns.quad9.net/dns-query"));
            _presets.Add(new DnsPreset("Quad9 (Uncensored)", "9.9.9.10", "149.112.112.10", "Security", "Uncensored, non-blocking Quad9 DNS without filtering.", "https://dns.quad9.net/dns-query"));
            _presets.Add(new DnsPreset("Control D (Malware)", "76.76.2.1", "76.76.10.1", "Security", "Blocks malware, phishing, and known malicious hosts.", "https://freedns.controld.com/p0"));
            _presets.Add(new DnsPreset("Control D (Unfiltered)", "76.76.2.0", "76.76.10.0", "Security", "High-performance unfiltered DNS by Control D.", "https://freedns.controld.com/p0"));
            _presets.Add(new DnsPreset("NextDNS", "45.90.28.0", "45.90.30.0", "Security", "Modern cloud DNS with advanced privacy protection.", "https://dns.nextdns.io"));
            _presets.Add(new DnsPreset("Cloudflare (Malware Block)", "1.1.1.2", "1.0.0.2", "Security", "1.1.1.1 with automated malware and threat blocking.", "https://cloudflare-dns.com/dns-query"));
            _presets.Add(new DnsPreset("Cloudflare (Family Safe)", "1.1.1.3", "1.0.0.3", "Security", "Blocks malware and adult content automatically.", "https://cloudflare-dns.com/dns-query"));
            _presets.Add(new DnsPreset("OpenDNS FamilyShield", "208.67.222.123", "208.67.220.123", "Security", "Pre-configured adult content blocking by OpenDNS.", "https://doh.opendns.com/dns-query"));
            _presets.Add(new DnsPreset("CleanBrowsing (Security)", "185.228.168.9", "185.228.169.9", "Security", "Blocks malware, phishing, and malicious domains.", "https://doh.cleanbrowsing.org/doh/security-filter/"));
            _presets.Add(new DnsPreset("CleanBrowsing (Family)", "185.228.168.168", "185.228.169.168", "Security", "Blocks adult content, phishing, and enforces SafeSearch.", "https://doh.cleanbrowsing.org/doh/family-filter/"));
            _presets.Add(new DnsPreset("Comodo Secure DNS", "8.26.56.26", "8.20.247.20", "Security", "Cloud-based security DNS by Comodo CyberSecurity."));

            // 3. Gaming & Unblock Resolvers
            _presets.Add(new DnsPreset("Radar Game", "10.202.10.10", "10.202.10.11", "Gaming", "Low-ping gaming DNS for reduced latency and packet loss."));
            _presets.Add(new DnsPreset("Electro DNS (Gaming)", "78.157.42.101", "78.157.42.100", "Gaming", "Optimized routing for gaming and regional service access."));
            _presets.Add(new DnsPreset("403.online", "10.202.10.202", "10.202.10.102", "Gaming", "Developer and service unblocker DNS for IT professionals."));
        }

        public List<DnsPreset> GetAllPresets()
        {
            return _presets.ToList();
        }

        public List<string> GetCategories()
        {
            var categories = new List<string>();
            categories.Add("All");
            categories.Add("General");
            categories.Add("Security");
            categories.Add("Gaming");
            return categories;
        }

        public void AddCustomPreset(string name, string primaryDns, string secondaryDns, string description)
        {
            var preset = new DnsPreset(name, primaryDns, secondaryDns, "General", description);
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

        public bool ExportCustomPresets(string filePath, out string message)
        {
            try
            {
                var customList = _presets.Where(delegate(DnsPreset p) { return p.IsCustom; }).ToList();
                if (customList.Count == 0)
                {
                    message = "No custom presets available to export.";
                    return false;
                }

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
                doc.Save(filePath);
                message = string.Format("Successfully exported {0} custom preset(s).", customList.Count);
                return true;
            }
            catch (Exception ex)
            {
                message = "Failed to export presets: " + ex.Message;
                return false;
            }
        }

        public bool ImportCustomPresets(string filePath, out string message, out int importedCount)
        {
            importedCount = 0;
            try
            {
                if (!File.Exists(filePath))
                {
                    message = "Selected file does not exist.";
                    return false;
                }

                var doc = XDocument.Load(filePath);
                if (doc.Root == null)
                {
                    message = "Invalid or empty presets file.";
                    return false;
                }

                var elements = doc.Root.Elements("Preset");
                int count = 0;
                foreach (var el in elements)
                {
                    string name = el.Element("Name") != null ? el.Element("Name").Value : "Custom DNS";
                    string primary = el.Element("PrimaryDns") != null ? el.Element("PrimaryDns").Value : "";
                    string secondary = el.Element("SecondaryDns") != null ? el.Element("SecondaryDns").Value : "";
                    string desc = el.Element("Description") != null ? el.Element("Description").Value : "Imported custom DNS";

                    if (!string.IsNullOrEmpty(primary))
                    {
                        bool exists = _presets.Any(delegate(DnsPreset p) { return string.Equals(p.PrimaryDns, primary, StringComparison.OrdinalIgnoreCase); });
                        if (!exists)
                        {
                            var preset = new DnsPreset(name, primary, secondary, "General", desc);
                            preset.IsCustom = true;
                            _presets.Add(preset);
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    SaveCustomPresets();
                    importedCount = count;
                    message = string.Format("Successfully imported {0} new preset(s).", count);
                    return true;
                }
                else
                {
                    message = "No new unique presets found in file (presets may already exist).";
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = "Failed to import presets: " + ex.Message;
                return false;
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
                        var preset = new DnsPreset(name, primary, secondary, "General", desc);
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
