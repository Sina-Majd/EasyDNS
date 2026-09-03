using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using EasyDNS.Models;

namespace EasyDNS.Services
{
    public class DnsManagerService
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache", SetLastError = true)]
        private static extern int DnsFlushResolverCache();

        /// <summary>
        /// Checks if current process is running with Windows Administrator privileges.
        /// </summary>
        public bool IsRunAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Flushes the Windows DNS Resolver cache via native API and ipconfig.
        /// </summary>
        public bool FlushDnsCache(out string message)
        {
            try
            {
                try
                {
                    DnsFlushResolverCache();
                }
                catch { }

                var psi = new ProcessStartInfo("ipconfig", "/flushdns");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);
                    message = string.IsNullOrWhiteSpace(output) ? "DNS Resolver Cache flushed successfully." : output.Trim();
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = "Failed to flush DNS cache: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Retrieves all network adapters suitable for DNS configuration.
        /// </summary>
        public List<NetworkAdapterInfo> GetNetworkAdapters()
        {
            var adapters = new List<NetworkAdapterInfo>();

            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                var wmiDict = GetWmiAdapterDetails();

                foreach (var nic in nics)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    if (nic.Description.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        nic.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    var info = new NetworkAdapterInfo();
                    info.Id = nic.Id;
                    info.Name = nic.Name;
                    info.Description = nic.Description;
                    info.TypeName = nic.NetworkInterfaceType.ToString();
                    info.IsOperational = (nic.OperationalStatus == OperationalStatus.Up);
                    info.MacAddress = nic.GetPhysicalAddress().ToString();

                    try
                    {
                        var ipProps = nic.GetIPProperties();
                        if (ipProps != null)
                        {
                            foreach (var uni in ipProps.UnicastAddresses)
                            {
                                if (uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    info.IpAddress = uni.Address.ToString();
                                    break;
                                }
                            }

                            foreach (var dns in ipProps.DnsAddresses)
                            {
                                if (dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    string dnsStr = dns.ToString();
                                    if (!info.DnsServers.Contains(dnsStr))
                                    {
                                        info.DnsServers.Add(dnsStr);
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    if (wmiDict.ContainsKey(nic.Description))
                    {
                        var wmi = wmiDict[nic.Description];
                        info.IsDhcpEnabled = wmi.IsDhcpEnabled;
                        if (info.DnsServers.Count == 0 && wmi.DnsServers.Count > 0)
                        {
                            info.DnsServers.AddRange(wmi.DnsServers);
                        }
                    }

                    adapters.Add(info);
                }
            }
            catch (Exception)
            {
                return GetAdaptersViaWmiOnly();
            }

            return adapters
                .OrderByDescending(delegate(NetworkAdapterInfo a) { return a.IsOperational; })
                .ThenByDescending(delegate(NetworkAdapterInfo a) { return a.TypeName == "Wireless80211" || a.TypeName == "Ethernet"; })
                .ToList();
        }

        private class WmiAdapterData
        {
            public bool IsDhcpEnabled { get; set; }
            public List<string> DnsServers { get; set; }

            public WmiAdapterData()
            {
                DnsServers = new List<string>();
            }
        }

        private Dictionary<string, WmiAdapterData> GetWmiAdapterDetails()
        {
            var dict = new Dictionary<string, WmiAdapterData>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Description, DHCPEnabled, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string desc = mo["Description"] as string;
                        if (!string.IsNullOrEmpty(desc))
                        {
                            var data = new WmiAdapterData();
                            if (mo["DHCPEnabled"] != null)
                            {
                                data.IsDhcpEnabled = (bool)mo["DHCPEnabled"];
                            }
                            string[] dnsArr = mo["DNSServerSearchOrder"] as string[];
                            if (dnsArr != null)
                            {
                                data.DnsServers.AddRange(dnsArr);
                            }
                            dict[desc] = data;
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        private List<NetworkAdapterInfo> GetAdaptersViaWmiOnly()
        {
            var list = new List<NetworkAdapterInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SettingID, Caption, Description, DHCPEnabled, DNSServerSearchOrder, IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var info = new NetworkAdapterInfo();
                        info.Id = mo["SettingID"] as string ?? Guid.NewGuid().ToString();
                        info.Name = mo["Caption"] as string ?? "Network Adapter";
                        info.Description = mo["Description"] as string ?? "";
                        info.TypeName = "Ethernet";
                        info.IsOperational = true;
                        info.IsDhcpEnabled = mo["DHCPEnabled"] != null && (bool)mo["DHCPEnabled"];

                        string[] ips = mo["IPAddress"] as string[];
                        if (ips != null && ips.Length > 0)
                        {
                            info.IpAddress = ips[0];
                        }

                        string[] dnsArr = mo["DNSServerSearchOrder"] as string[];
                        if (dnsArr != null)
                        {
                            info.DnsServers.AddRange(dnsArr);
                        }

                        list.Add(info);
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Sets static DNS addresses on the specified adapter.
        /// </summary>
        public bool SetDns(NetworkAdapterInfo adapter, string primaryDns, string secondaryDns, out string resultMessage)
        {
            if (adapter == null)
            {
                resultMessage = "No network adapter selected.";
                return false;
            }

            var servers = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryDns)) servers.Add(primaryDns.Trim());
            if (!string.IsNullOrWhiteSpace(secondaryDns)) servers.Add(secondaryDns.Trim());

            if (servers.Count == 0)
            {
                resultMessage = "Please specify at least a Primary DNS server.";
                return false;
            }

            bool wmiSuccess = false;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string desc = mo["Description"] as string;
                        string settingId = mo["SettingID"] as string;

                        if (string.Equals(desc, adapter.Description, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(settingId, adapter.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var inParams = mo.GetMethodParameters("SetDNSServerSearchOrder"))
                            {
                                inParams["DNSServerSearchOrder"] = servers.ToArray();
                                var outParams = mo.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                                uint retVal = (uint)outParams["ReturnValue"];
                                if (retVal == 0 || retVal == 1)
                                {
                                    wmiSuccess = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            bool netshSuccess = false;
            string netshError = string.Empty;
            if (!wmiSuccess)
            {
                netshSuccess = SetDnsViaNetsh(adapter.Name, servers, out netshError);
            }

            if (wmiSuccess || netshSuccess)
            {
                string dummy;
                FlushDnsCache(out dummy);
                resultMessage = string.Format("DNS successfully set to {0} on '{1}'.", string.Join(", ", servers.ToArray()), adapter.Name);
                return true;
            }
            else
            {
                resultMessage = "Failed to update DNS settings: " + (string.IsNullOrWhiteSpace(netshError) ? "Please ensure the app is run as Administrator." : netshError);
                return false;
            }
        }

        /// <summary>
        /// Sets DNS to automatic (DHCP) for the specified adapter.
        /// </summary>
        public bool ResetToDhcp(NetworkAdapterInfo adapter, out string resultMessage)
        {
            if (adapter == null)
            {
                resultMessage = "No network adapter selected.";
                return false;
            }

            bool wmiSuccess = false;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string desc = mo["Description"] as string;
                        string settingId = mo["SettingID"] as string;

                        if (string.Equals(desc, adapter.Description, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(settingId, adapter.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var inParams = mo.GetMethodParameters("SetDNSServerSearchOrder"))
                            {
                                inParams["DNSServerSearchOrder"] = null;
                                var outParams = mo.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                                uint retVal = (uint)outParams["ReturnValue"];
                                if (retVal == 0 || retVal == 1)
                                {
                                    wmiSuccess = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            bool netshSuccess = false;
            string netshError = string.Empty;
            if (!wmiSuccess)
            {
                netshSuccess = ResetDnsViaNetsh(adapter.Name, out netshError);
            }

            if (wmiSuccess || netshSuccess)
            {
                string dummy;
                FlushDnsCache(out dummy);
                resultMessage = string.Format("DNS reset to default on '{0}'.", adapter.Name);
                return true;
            }
            else
            {
                resultMessage = "Failed to reset DNS: " + (string.IsNullOrWhiteSpace(netshError) ? "Please run as Administrator." : netshError);
                return false;
            }
        }

        private bool SetDnsViaNetsh(string adapterName, List<string> servers, out string errorMsg)
        {
            errorMsg = string.Empty;
            try
            {
                if (servers != null && servers.Count > 0)
                {
                    string safeAdapterName = adapterName.Replace("\"", "\\\"");
                    string cmd1 = string.Format("interface ipv4 set dns name=\"{0}\" source=static address={1} register=primary", safeAdapterName, servers[0]);
                    string out1, err1;
                    bool ok1 = RunProcess("netsh", cmd1, out out1, out err1);
                    if (!ok1)
                    {
                        errorMsg = !string.IsNullOrWhiteSpace(err1) ? err1.Trim() : (!string.IsNullOrWhiteSpace(out1) ? out1.Trim() : "netsh command failed");
                        return false;
                    }

                    if (servers.Count > 1)
                    {
                        string cmd2 = string.Format("interface ipv4 add dns name=\"{0}\" address={1} index=2", safeAdapterName, servers[1]);
                        string out2, err2;
                        bool ok2 = RunProcess("netsh", cmd2, out out2, out err2);
                        if (!ok2)
                        {
                            errorMsg = !string.IsNullOrWhiteSpace(err2) ? err2.Trim() : (!string.IsNullOrWhiteSpace(out2) ? out2.Trim() : "Failed to add secondary DNS server");
                            return false;
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
            }
            return false;
        }

        private bool ResetDnsViaNetsh(string adapterName, out string errorMsg)
        {
            errorMsg = string.Empty;
            try
            {
                string safeAdapterName = adapterName.Replace("\"", "\\\"");
                string cmd = string.Format("interface ipv4 set dns name=\"{0}\" source=dhcp", safeAdapterName);
                string out1, err1;
                bool ok = RunProcess("netsh", cmd, out out1, out err1);
                if (!ok)
                {
                    errorMsg = !string.IsNullOrWhiteSpace(err1) ? err1.Trim() : (!string.IsNullOrWhiteSpace(out1) ? out1.Trim() : "netsh DHCP reset failed");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
            }
            return false;
        }

        private static bool RunProcess(string filename, string arguments, out string output, out string error)
        {
            output = string.Empty;
            error = string.Empty;
            try
            {
                var psi = new ProcessStartInfo(filename, arguments);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        error = "Failed to launch process: " + filename;
                        return false;
                    }
                    output = proc.StandardOutput.ReadToEnd();
                    error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(4000);
                    return proc.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Validates if an IP string is a valid IPv4 address (strictly 4 octets, 0-255).
        /// </summary>
        public static bool IsValidIpv4(string ipString)
        {
            if (string.IsNullOrWhiteSpace(ipString)) return false;
            string trimmed = ipString.Trim();
            string[] parts = trimmed.Split('.');
            if (parts.Length != 4) return false;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                byte b;
                if (!byte.TryParse(part, out b)) return false;
                if (part.Length > 1 && part.StartsWith("0")) return false;
            }

            IPAddress address;
            if (IPAddress.TryParse(trimmed, out address))
            {
                return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
            }
            return false;
        }

        /// <summary>
        /// Checks if current operating system is Windows 11 or newer (Build >= 22000).
        /// </summary>
        public static bool IsWindows11OrGreater()
        {
            try
            {
                var os = Environment.OSVersion;
                return os.Platform == PlatformID.Win32NT &&
                       os.Version.Major >= 10 &&
                       os.Version.Build >= 22000;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Configures native Windows 11 DNS-over-HTTPS (DoH) encryption for a given adapter and resolver IP.
        /// </summary>
        public bool ConfigureDoH(NetworkAdapterInfo adapter, string dnsIp, string dohTemplate, out string message)
        {
            message = string.Empty;
            if (adapter == null || string.IsNullOrWhiteSpace(dnsIp) || string.IsNullOrWhiteSpace(dohTemplate))
            {
                message = "Invalid adapter, IP, or DoH template.";
                return false;
            }

            if (!IsWindows11OrGreater())
            {
                message = "Native DNS-over-HTTPS requires Windows 11 (Build 22000+).";
                return false;
            }

            try
            {
                string safeAdapterName = adapter.Name.Replace("\"", "\\\"");
                string cmd = string.Format("dns add encryption interface=\"{0}\" ip={1} dohtemplate=\"{2}\" autoupgrade=yes",
                    safeAdapterName, dnsIp.Trim(), dohTemplate.Trim());

                string output, error;
                bool success = RunProcess("netsh", cmd, out output, out error);
                if (success)
                {
                    message = "DNS-over-HTTPS encryption configured for " + dnsIp + " on '" + adapter.Name + "'.";
                    return true;
                }
                else
                {
                    message = "Failed to configure DoH: " + (!string.IsNullOrWhiteSpace(error) ? error.Trim() : output.Trim());
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = "DoH configuration error: " + ex.Message;
                return false;
            }
        }
    }
}
