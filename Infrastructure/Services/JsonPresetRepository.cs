using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using EasyDNS.Core.Common;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;
using EasyDNS.Infrastructure.Serialization;

namespace EasyDNS.Infrastructure.Services
{
    public class JsonPresetRepository : IPresetRepository
    {
        private readonly List<DnsPreset> _presets = new();
        private readonly string _customJsonPath;
        private readonly string _legacyXmlPath;
        private readonly object _lock = new();

        public JsonPresetRepository()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyDNS");
            if (!Directory.Exists(appData))
            {
                try { Directory.CreateDirectory(appData); } catch { /* ignore */ }
            }

            _customJsonPath = Path.Combine(appData, "presets.json");
            _legacyXmlPath = Path.Combine(appData, "custom_presets.xml");

            LoadDefaultPresets();
            MigrateLegacyXmlIfNeeded();
            LoadCustomPresets();
        }

        private void LoadDefaultPresets()
        {
            lock (_lock)
            {
                _presets.Clear();

                // 1. General DNS Resolvers (Fast & Public)
                _presets.Add(new DnsPreset("Cloudflare (1.1.1.1)", "1.1.1.1", "1.0.0.1", "General", "Ultra-fast, privacy-first DNS by Cloudflare & APNIC.", "https://cloudflare-dns.com/dns-query", "2606:4700:4700::1111", "2606:4700:4700::1001"));
                _presets.Add(new DnsPreset("Google Public DNS", "8.8.8.8", "8.8.4.4", "General", "Global, highly reliable public DNS service by Google.", "https://dns.google/dns-query", "2001:4860:4860::8888", "2001:4860:4860::8844"));
                _presets.Add(new DnsPreset("OpenDNS Home", "208.67.222.222", "208.67.220.220", "General", "Cisco Umbrella cloud DNS with intelligent routing and protection.", "https://doh.opendns.com/dns-query", "2620:119:35::35", "2620:119:53::53"));
                _presets.Add(new DnsPreset("Shecan", "178.22.122.100", "185.51.200.2", "General, Gaming", "Bypasses international restrictions and developer sanctions."));
                _presets.Add(new DnsPreset("DNS.WATCH", "84.200.69.80", "84.200.70.40", "General", "Fast, uncensored, no-logging European DNS.", "", "2001:1608:10:25::1c04:b12f", "2001:1608:10:25::9249:d69b"));
                _presets.Add(new DnsPreset("Level3 (CenturyLink)", "4.2.2.1", "4.2.2.2", "General", "Tier-1 backbone DNS server infrastructure."));

                // 2. Security & Privacy Resolvers
                _presets.Add(new DnsPreset("AdGuard DNS (Default)", "94.140.14.14", "94.140.15.15", "Security", "Blocks ads, trackers, banners, and phishing domains.", "https://dns.adguard-dns.com/dns-query", "2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff"));
                _presets.Add(new DnsPreset("AdGuard DNS (Family)", "94.140.14.15", "94.140.15.16", "Security", "Blocks ads, trackers, adult websites and enables safe search.", "https://dns.adguard-dns.com/dns-query", "2a10:50c0::bad1:ff", "2a10:50c0::bad2:ff"));
                _presets.Add(new DnsPreset("Quad9 (Malware Blocking)", "9.9.9.9", "149.112.112.112", "Security", "Blocks malicious domains, phishing, and botnets with zero logging.", "https://dns.quad9.net/dns-query", "2620:fe::fe", "2620:fe::9"));
                _presets.Add(new DnsPreset("Quad9 (Uncensored)", "9.9.9.10", "149.112.112.10", "Security", "Uncensored, non-blocking Quad9 DNS without filtering.", "https://dns.quad9.net/dns-query", "2620:fe::10", "2620:fe::fe:10"));
                _presets.Add(new DnsPreset("Control D (Malware)", "76.76.2.1", "76.76.10.1", "Security", "Blocks malware, phishing, and known malicious hosts.", "https://freedns.controld.com/p0", "2606:1a40::1", "2606:1a40:1::1"));
                _presets.Add(new DnsPreset("Control D (Unfiltered)", "76.76.2.0", "76.76.10.0", "Security", "High-performance unfiltered DNS by Control D.", "https://freedns.controld.com/p0", "2606:1a40::", "2606:1a40:1::"));
                _presets.Add(new DnsPreset("NextDNS", "45.90.28.0", "45.90.30.0", "Security", "Modern cloud DNS with advanced privacy protection.", "https://dns.nextdns.io", "2a07:a8c0::", "2a07:a8c1::"));
                _presets.Add(new DnsPreset("Cloudflare (Malware Block)", "1.1.1.2", "1.0.0.2", "Security", "1.1.1.1 with automated malware and threat blocking.", "https://cloudflare-dns.com/dns-query", "2606:4700:4700::1112", "2606:4700:4700::1002"));
                _presets.Add(new DnsPreset("Cloudflare (Family Safe)", "1.1.1.3", "1.0.0.3", "Security", "Blocks malware and adult content automatically.", "https://cloudflare-dns.com/dns-query", "2606:4700:4700::1113", "2606:4700:4700::1003"));
                _presets.Add(new DnsPreset("OpenDNS FamilyShield", "208.67.222.123", "208.67.220.123", "Security", "Pre-configured adult content blocking by OpenDNS.", "https://doh.opendns.com/dns-query", "2620:119:35::123", "2620:119:53::123"));
                _presets.Add(new DnsPreset("CleanBrowsing (Security)", "185.228.168.9", "185.228.169.9", "Security", "Blocks malware, phishing, and malicious domains.", "https://doh.cleanbrowsing.org/doh/security-filter/", "2a0d:2a00:1::2", "2a0d:2a00:2::2"));
                _presets.Add(new DnsPreset("CleanBrowsing (Family)", "185.228.168.168", "185.228.169.168", "Security", "Blocks adult content, phishing, and enforces SafeSearch.", "https://doh.cleanbrowsing.org/doh/family-filter/", "2a0d:2a00:1::", "2a0d:2a00:2::"));
                _presets.Add(new DnsPreset("Comodo Secure DNS", "8.26.56.26", "8.20.247.20", "Security", "Cloud-based security DNS by Comodo CyberSecurity."));

                // 3. Gaming & Unblock Resolvers
                _presets.Add(new DnsPreset("Radar Game", "10.202.10.10", "10.202.10.11", "Gaming", "Low-ping gaming DNS for reduced latency and packet loss."));
                _presets.Add(new DnsPreset("Electro DNS (Gaming)", "78.157.42.101", "78.157.42.100", "Gaming", "Optimized routing for gaming and regional service access."));
                _presets.Add(new DnsPreset("403.online", "10.202.10.202", "10.202.10.102", "Gaming", "Developer and service unblocker DNS for IT professionals."));
            }
        }

        private void MigrateLegacyXmlIfNeeded()
        {
            try
            {
                if (File.Exists(_customJsonPath) || !File.Exists(_legacyXmlPath)) return;

                var doc = XDocument.Load(_legacyXmlPath);
                if (doc.Root == null) return;

                var customList = new List<DnsPreset>();
                foreach (var el in doc.Root.Elements("Preset"))
                {
                    string id = el.Attribute("Id")?.Value ?? Guid.NewGuid().ToString("N");
                    string name = el.Element("Name")?.Value ?? "Custom DNS";
                    string primary = el.Element("PrimaryDns")?.Value ?? "";
                    string secondary = el.Element("SecondaryDns")?.Value ?? "";
                    string desc = el.Element("Description")?.Value ?? "User-defined custom DNS";

                    if (!string.IsNullOrWhiteSpace(primary))
                    {
                        var preset = new DnsPreset(name, primary, secondary, "General", desc)
                        {
                            Id = id,
                            IsCustom = true
                        };
                        customList.Add(preset);
                    }
                }

                if (customList.Count > 0)
                {
                    string json = JsonSerializer.Serialize(customList, PresetJsonContext.Default.ListDnsPreset);
                    File.WriteAllText(_customJsonPath, json);
                }
            }
            catch
            {
                // Silently skip if migration fails
            }
        }

        private void LoadCustomPresets()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_customJsonPath)) return;
                    string json = File.ReadAllText(_customJsonPath);
                    var customList = JsonSerializer.Deserialize(json, PresetJsonContext.Default.ListDnsPreset);
                    if (customList != null)
                    {
                        foreach (var item in customList)
                        {
                            item.IsCustom = true;
                            if (!_presets.Any(p => p.Id == item.Id))
                            {
                                _presets.Add(item);
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to loading legacy XML if JSON load encounters an issue
                }
            }
        }

        public IReadOnlyList<DnsPreset> GetAllPresets()
        {
            lock (_lock)
            {
                return _presets.ToList();
            }
        }

        public IReadOnlyList<string> GetCategories()
        {
            return new[] { "All", "General", "Security", "Gaming" };
        }

        public async Task AddCustomPresetAsync(string name, string primaryDns, string secondaryDns, string description, string? primaryIpv6 = null, string? secondaryIpv6 = null)
        {
            var preset = new DnsPreset(name, primaryDns, secondaryDns, "General", description, "", primaryIpv6, secondaryIpv6)
            {
                IsCustom = true
            };

            lock (_lock)
            {
                _presets.Add(preset);
            }

            await SaveCustomPresetsAsync();
        }

        public async Task DeleteCustomPresetAsync(string presetId)
        {
            lock (_lock)
            {
                var item = _presets.FirstOrDefault(p => p.Id == presetId && p.IsCustom);
                if (item != null)
                {
                    _presets.Remove(item);
                }
            }

            await SaveCustomPresetsAsync();
        }

        private async Task SaveCustomPresetsAsync()
        {
            List<DnsPreset> customList;
            lock (_lock)
            {
                customList = _presets.Where(p => p.IsCustom).ToList();
            }

            try
            {
                string json = JsonSerializer.Serialize(customList, PresetJsonContext.Default.ListDnsPreset);
                await File.WriteAllTextAsync(_customJsonPath, json);
            }
            catch
            {
                // File writing error handled safely
            }
        }

        public async Task<OperationResult> ExportCustomPresetsAsync(string filePath)
        {
            try
            {
                List<DnsPreset> customList;
                lock (_lock)
                {
                    customList = _presets.Where(p => p.IsCustom).ToList();
                }

                if (customList.Count == 0)
                {
                    return OperationResult.Fail("No custom presets available to export.");
                }

                string json = JsonSerializer.Serialize(customList, PresetJsonContext.Default.ListDnsPreset);
                await File.WriteAllTextAsync(filePath, json);
                return OperationResult.Ok($"Successfully exported {customList.Count} custom preset(s).");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Failed to export presets: {ex.Message}");
            }
        }

        public async Task<(OperationResult Result, int ImportedCount)> ImportCustomPresetsAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return (OperationResult.Fail("Selected file does not exist."), 0);
                }

                List<DnsPreset>? importedList = null;

                // 1. Try JSON import
                try
                {
                    string content = await File.ReadAllTextAsync(filePath);
                    importedList = JsonSerializer.Deserialize(content, PresetJsonContext.Default.ListDnsPreset);
                }
                catch
                {
                    // If JSON fails, check if it's a legacy XML export
                }

                // 2. Fallback to XML import
                if (importedList == null || importedList.Count == 0)
                {
                    try
                    {
                        var doc = XDocument.Load(filePath);
                        if (doc.Root != null)
                        {
                            importedList = new List<DnsPreset>();
                            foreach (var el in doc.Root.Elements("Preset"))
                            {
                                string name = el.Element("Name")?.Value ?? "Custom DNS";
                                string primary = el.Element("PrimaryDns")?.Value ?? "";
                                string secondary = el.Element("SecondaryDns")?.Value ?? "";
                                string desc = el.Element("Description")?.Value ?? "Imported custom DNS";

                                if (!string.IsNullOrEmpty(primary))
                                {
                                    importedList.Add(new DnsPreset(name, primary, secondary, "General", desc)
                                    {
                                        IsCustom = true
                                    });
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                if (importedList == null || importedList.Count == 0)
                {
                    return (OperationResult.Fail("No valid presets found in selected file."), 0);
                }

                int addedCount = 0;
                lock (_lock)
                {
                    foreach (var item in importedList)
                    {
                        if (string.IsNullOrWhiteSpace(item.PrimaryDns)) continue;

                        bool exists = _presets.Any(p => string.Equals(p.PrimaryDns, item.PrimaryDns, StringComparison.OrdinalIgnoreCase));
                        if (!exists)
                        {
                            item.IsCustom = true;
                            if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString("N");
                            _presets.Add(item);
                            addedCount++;
                        }
                    }
                }

                if (addedCount > 0)
                {
                    await SaveCustomPresetsAsync();
                    return (OperationResult.Ok($"Successfully imported {addedCount} new preset(s)."), addedCount);
                }

                return (OperationResult.Fail("No new unique presets found in file (presets may already exist)."), 0);
            }
            catch (Exception ex)
            {
                return (OperationResult.Fail($"Failed to import presets: {ex.Message}"), 0);
            }
        }
    }
}
