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

            bool netshSuccess = SetDnsViaNetsh(adapter.Name, servers);

            if (wmiSuccess || netshSuccess)
            {
                string dummy;
                FlushDnsCache(out dummy);
                resultMessage = string.Format("DNS successfully set to {0} on '{1}'.", string.Join(", ", servers.ToArray()), adapter.Name);
                return true;
            }
            else
            {
                resultMessage = "Failed to update DNS settings. Please ensure the app is run as Administrator.";
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

            bool netshSuccess = ResetDnsViaNetsh(adapter.Name);

            if (wmiSuccess || netshSuccess)
            {
                string dummy;
                FlushDnsCache(out dummy);
                resultMessage = string.Format("DNS reset to Automatic (DHCP) on '{0}'.", adapter.Name);
                return true;
            }
            else
            {
                resultMessage = "Failed to reset DNS to DHCP. Please run as Administrator.";
                return false;
            }
        }

        private bool SetDnsViaNetsh(string adapterName, List<string> servers)
        {
            try
            {
                if (servers.Count > 0)
                {
                    string cmd = string.Format("interface ipv4 set dns name=\"{0}\" source=static address={1} register=primary", adapterName, servers[0]);
                    RunProcess("netsh", cmd);

                    if (servers.Count > 1)
                    {
                        string cmd2 = string.Format("interface ipv4 add dns name=\"{0}\" address={1} index=2", adapterName, servers[1]);
                        RunProcess("netsh", cmd2);
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool ResetDnsViaNetsh(string adapterName)
        {
            try
            {
                string cmd = string.Format("interface ipv4 set dns name=\"{0}\" source=dhcp", adapterName);
                RunProcess("netsh", cmd);
                return true;
            }
            catch { }
            return false;
        }

        private static void RunProcess(string filename, string arguments)
        {
            var psi = new ProcessStartInfo(filename, arguments);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var proc = Process.Start(psi))
            {
                proc.WaitForExit(3000);
            }
        }

        /// <summary>
        /// Validates if an IP string is a valid IPv4 address.
        /// </summary>
        public static bool IsValidIpv4(string ipString)
        {
            if (string.IsNullOrWhiteSpace(ipString)) return false;
            IPAddress address;
            if (IPAddress.TryParse(ipString.Trim(), out address))
            {
                return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
            }
            return false;
        }
    }
}
