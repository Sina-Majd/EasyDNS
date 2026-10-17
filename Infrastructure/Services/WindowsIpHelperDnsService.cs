using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyDNS.Core.Common;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;

namespace EasyDNS.Infrastructure.Services
{
    public class WindowsIpHelperDnsService : INetworkDnsService
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache", SetLastError = true)]
        private static extern int DnsFlushResolverCache();

        public bool IsRunAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public IReadOnlyList<NetworkAdapterInfo> GetNetworkAdapters()
        {
            var adapters = new List<NetworkAdapterInfo>();

            try
            {
                var controlPanelKeys = GetControlPanelAdapterKeys();
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                var wmiDict = GetWmiAdapterDetails();

                foreach (var nic in nics)
                {
                    if (controlPanelKeys.Count > 0)
                    {
                        // Match with Control Panel Network Connections (MSFT_NetAdapter)
                        if (!controlPanelKeys.Contains(nic.Id) &&
                            !controlPanelKeys.Contains(nic.Name) &&
                            !controlPanelKeys.Contains(nic.Description))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // Fallback heuristic filter matching Control Panel adapters
                        if (!IsValidControlPanelAdapter(nic))
                        {
                            continue;
                        }
                    }

                    var info = new NetworkAdapterInfo
                    {
                        Id = nic.Id,
                        Name = nic.Name,
                        Description = nic.Description,
                        TypeName = nic.NetworkInterfaceType.ToString(),
                        IsOperational = nic.OperationalStatus == OperationalStatus.Up,
                        MacAddress = nic.GetPhysicalAddress().ToString()
                    };

                    try
                    {
                        var ipProps = nic.GetIPProperties();
                        if (ipProps != null)
                        {
                            foreach (var uni in ipProps.UnicastAddresses)
                            {
                                if (uni.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    info.IpAddress = uni.Address.ToString();
                                    break;
                                }
                            }

                            foreach (var dns in ipProps.DnsAddresses)
                            {
                                string dnsStr = dns.ToString();
                                if (dns.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    if (!info.DnsServers.Contains(dnsStr))
                                    {
                                        info.DnsServers.Add(dnsStr);
                                    }
                                }
                                else if (dns.AddressFamily == AddressFamily.InterNetworkV6)
                                {
                                    if (!info.DnsServersIpv6.Contains(dnsStr))
                                    {
                                        info.DnsServersIpv6.Add(dnsStr);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore IP properties read failure for special interfaces
                    }

                    if (wmiDict.TryGetValue(nic.Id, out var wmi) || wmiDict.TryGetValue(nic.Description, out wmi))
                    {
                        info.IsDhcpEnabled = wmi.IsDhcpEnabled;
                        if (info.DnsServers.Count == 0 && wmi.DnsServers.Count > 0)
                        {
                            info.DnsServers.AddRange(wmi.DnsServers);
                        }
                    }

                    // Determine if DNS is configured automatically (DHCP) or manually specified (static)
                    bool isStaticDns = false;
                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                            $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{nic.Id}");
                        if (key != null)
                        {
                            string? nameServer = key.GetValue("NameServer") as string;
                            if (!string.IsNullOrWhiteSpace(nameServer))
                            {
                                isStaticDns = true;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore registry read errors
                    }

                    if (!isStaticDns)
                    {
                        try
                        {
                            using var key6 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                                $@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\{nic.Id}");
                            if (key6 != null)
                            {
                                string? nameServer6 = key6.GetValue("NameServer") as string;
                                if (!string.IsNullOrWhiteSpace(nameServer6))
                                {
                                    isStaticDns = true;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore registry read errors
                        }
                    }

                    info.IsDnsAutomatic = !isStaticDns;

                    adapters.Add(info);
                }
            }
            catch
            {
                return GetAdaptersViaWmiOnly();
            }

            return adapters
                .OrderByDescending(a => a.IsOperational)
                .ThenByDescending(a => a.TypeName == "Wireless80211" || a.TypeName == "Ethernet")
                .ThenBy(a => a.Name)
                .ToList();
        }

        private static HashSet<string> GetControlPanelAdapterKeys()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT DeviceID, Name, InterfaceDescription FROM MSFT_NetAdapter");
                foreach (ManagementObject mo in searcher.Get())
                {
                    if (mo["DeviceID"] is string devId && !string.IsNullOrWhiteSpace(devId))
                    {
                        keys.Add(devId);
                    }
                    if (mo["Name"] is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        keys.Add(name);
                    }
                    if (mo["InterfaceDescription"] is string desc && !string.IsNullOrWhiteSpace(desc))
                    {
                        keys.Add(desc);
                    }
                }
            }
            catch
            {
                // Silently fallback if MSFT_NetAdapter is not accessible
            }
            return keys;
        }

        private static bool IsValidControlPanelAdapter(NetworkInterface nic)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Unknown)
            {
                return false;
            }

            string desc = nic.Description ?? string.Empty;
            string name = nic.Name ?? string.Empty;

            // Filter out WAN Miniport pseudo-adapters (IKEv2, L2TP, PPTP, PPPOE, IP, IPv6, SSTP, etc.)
            if (desc.StartsWith("WAN Miniport", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("WAN Miniport", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Filter out Microsoft Wi-Fi Direct and Hosted Network virtual adapters
            if (desc.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("Hosted Network", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Filter out Kernel Debugger, Npcap / WinPcap packet capture loopbacks
            if (desc.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("Npcap Loopback", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Filter out transition tunnels (Teredo, 6to4, ISATAP, IP-HTTPS)
            if (desc.Contains("Teredo", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("ISATAP", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("6to4", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("IP-HTTPS", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Filter out Bluetooth if disconnected (Control Panel hides Bluetooth PAN unless paired/connected)
            if (desc.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
                nic.OperationalStatus != OperationalStatus.Up)
            {
                return false;
            }

            return true;
        }

        private record WmiAdapterData(bool IsDhcpEnabled, List<string> DnsServers);

        private static Dictionary<string, WmiAdapterData> GetWmiAdapterDetails()
        {
            var dict = new Dictionary<string, WmiAdapterData>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT SettingID, Description, DHCPEnabled, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                foreach (ManagementObject mo in searcher.Get())
                {
                    bool dhcp = mo["DHCPEnabled"] is bool b && b;
                    var dnsList = new List<string>();
                    if (mo["DNSServerSearchOrder"] is string[] dnsArr)
                    {
                        dnsList.AddRange(dnsArr);
                    }
                    var data = new WmiAdapterData(dhcp, dnsList);

                    if (mo["Description"] is string desc && !string.IsNullOrEmpty(desc))
                    {
                        dict[desc] = data;
                    }
                    if (mo["SettingID"] is string id && !string.IsNullOrEmpty(id))
                    {
                        dict[id] = data;
                    }
                }
            }
            catch
            {
                // Silently handle WMI unavailability
            }
            return dict;
        }

        private static List<NetworkAdapterInfo> GetAdaptersViaWmiOnly()
        {
            var list = new List<NetworkAdapterInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT SettingID, Caption, Description, DHCPEnabled, DNSServerSearchOrder, IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var info = new NetworkAdapterInfo
                    {
                        Id = mo["SettingID"] as string ?? Guid.NewGuid().ToString(),
                        Name = mo["Caption"] as string ?? "Network Adapter",
                        Description = mo["Description"] as string ?? "",
                        TypeName = "Ethernet",
                        IsOperational = true,
                        IsDhcpEnabled = mo["DHCPEnabled"] is bool b && b
                    };

                    if (mo["IPAddress"] is string[] ips && ips.Length > 0)
                    {
                        info.IpAddress = ips[0];
                    }

                    if (mo["DNSServerSearchOrder"] is string[] dnsArr)
                    {
                        info.DnsServers.AddRange(dnsArr);
                    }

                    list.Add(info);
                }
            }
            catch
            {
                // Return empty list on complete WMI failure
            }
            return list;
        }

        public async Task<OperationResult> SetDnsAsync(
            NetworkAdapterInfo adapter,
            string primaryDns,
            string? secondaryDns,
            string? dohTemplate = null,
            string? primaryIpv6 = null,
            string? secondaryIpv6 = null)
        {
            if (adapter == null)
            {
                return OperationResult.Fail("No network adapter selected.");
            }

            var servers = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryDns)) servers.Add(primaryDns.Trim());
            if (!string.IsNullOrWhiteSpace(secondaryDns)) servers.Add(secondaryDns.Trim());

            if (servers.Count == 0)
            {
                return OperationResult.Fail("Please specify at least a Primary DNS server.");
            }

            // 1. Try modern PowerShell Set-DnsClientServerAddress cmdlet
            string serverArgs = string.Join(",", servers.Select(s => $"\"{s}\""));
            if (!string.IsNullOrWhiteSpace(primaryIpv6)) serverArgs += $",\"{primaryIpv6.Trim()}\"";
            if (!string.IsNullOrWhiteSpace(secondaryIpv6)) serverArgs += $",\"{secondaryIpv6.Trim()}\"";

            string safeAlias = adapter.Name.Replace("'", "''");
            string psScript = $"Set-DnsClientServerAddress -InterfaceAlias '{safeAlias}' -ServerAddresses @({serverArgs})";
            var psResult = await RunPowerShellAsync(psScript);

            bool success = psResult.Success;

            // 2. Fallback to WMI SetDNSServerSearchOrder if PowerShell failed
            if (!success)
            {
                success = SetDnsViaWmi(adapter, servers);
            }

            // 3. Fallback to netsh if WMI also failed
            if (!success)
            {
                success = SetDnsViaNetsh(adapter.Name, servers, primaryIpv6, secondaryIpv6);
            }

            if (success)
            {
                // Configure Windows 11 DoH if template provided and on Windows 11
                if (!string.IsNullOrWhiteSpace(dohTemplate) && IsWindows11OrGreater())
                {
                    await ConfigureDoHAsync(adapter, primaryDns, dohTemplate);
                }

                await FlushDnsCacheAsync();
                return OperationResult.Ok($"DNS successfully set to {string.Join(", ", servers)} on '{adapter.Name}'.");
            }

            return OperationResult.Fail($"Failed to set DNS on '{adapter.Name}'. Please ensure EasyDNS is running as Administrator.");
        }

        public async Task<OperationResult> ResetToDhcpAsync(NetworkAdapterInfo adapter)
        {
            if (adapter == null)
            {
                return OperationResult.Fail("No network adapter selected.");
            }

            // 1. Try PowerShell ResetServerAddresses
            string safeAlias = adapter.Name.Replace("'", "''");
            string psScript = $"Set-DnsClientServerAddress -InterfaceAlias '{safeAlias}' -ResetServerAddresses";
            var psResult = await RunPowerShellAsync(psScript);

            bool success = psResult.Success;

            // 2. Fallback to WMI
            if (!success)
            {
                success = ResetDnsViaWmi(adapter);
            }

            // 3. Fallback to netsh
            if (!success)
            {
                success = ResetDnsViaNetsh(adapter.Name);
            }

            if (success)
            {
                await FlushDnsCacheAsync();
                return OperationResult.Ok($"DNS successfully reset to default (DHCP) on '{adapter.Name}'.");
            }

            return OperationResult.Fail($"Failed to reset DNS on '{adapter.Name}'. Please run EasyDNS as Administrator.");
        }

        public async Task<OperationResult> FlushDnsCacheAsync()
        {
            try
            {
                DnsFlushResolverCache();
            }
            catch
            {
                // Ignore native call failure and rely on ipconfig
            }

            try
            {
                var (success, output) = await RunProcessAsync("ipconfig", "/flushdns", timeoutMs: 3000);
                string msg = string.IsNullOrWhiteSpace(output)
                    ? "DNS Resolver Cache flushed successfully."
                    : output.Trim();
                return success ? OperationResult.Ok(msg) : OperationResult.Fail(msg);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Failed to flush DNS cache: {ex.Message}");
            }
        }

        private static bool SetDnsViaWmi(NetworkAdapterInfo adapter, List<string> servers)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string? desc = mo["Description"] as string;
                    string? settingId = mo["SettingID"] as string;

                    if (string.Equals(desc, adapter.Description, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(settingId, adapter.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        using var inParams = mo.GetMethodParameters("SetDNSServerSearchOrder");
                        inParams["DNSServerSearchOrder"] = servers.ToArray();
                        var outParams = mo.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                        uint retVal = (uint)outParams["ReturnValue"];
                        if (retVal == 0 || retVal == 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Silently fall through
            }
            return false;
        }

        private static bool ResetDnsViaWmi(NetworkAdapterInfo adapter)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string? desc = mo["Description"] as string;
                    string? settingId = mo["SettingID"] as string;

                    if (string.Equals(desc, adapter.Description, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(settingId, adapter.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        using var inParams = mo.GetMethodParameters("SetDNSServerSearchOrder");
                        inParams["DNSServerSearchOrder"] = null;
                        var outParams = mo.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                        uint retVal = (uint)outParams["ReturnValue"];
                        if (retVal == 0 || retVal == 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Silently fall through
            }
            return false;
        }

        private static bool SetDnsViaNetsh(string adapterName, List<string> servers, string? primaryIpv6 = null, string? secondaryIpv6 = null)
        {
            try
            {
                string safeName = adapterName.Replace("\"", "\\\"");
                string cmd1 = $"interface ipv4 set dns name=\"{safeName}\" source=static address={servers[0]} register=primary";
                bool ok1 = RunProcess("netsh", cmd1, out _, out _);
                if (!ok1) return false;

                if (servers.Count > 1)
                {
                    string cmd2 = $"interface ipv4 add dns name=\"{safeName}\" address={servers[1]} index=2";
                    RunProcess("netsh", cmd2, out _, out _);
                }

                if (!string.IsNullOrWhiteSpace(primaryIpv6))
                {
                    string cmdV6_1 = $"interface ipv6 set dnsservers name=\"{safeName}\" source=static address={primaryIpv6.Trim()} register=primary";
                    RunProcess("netsh", cmdV6_1, out _, out _);

                    if (!string.IsNullOrWhiteSpace(secondaryIpv6))
                    {
                        string cmdV6_2 = $"interface ipv6 add dnsservers name=\"{safeName}\" address={secondaryIpv6.Trim()} index=2";
                        RunProcess("netsh", cmdV6_2, out _, out _);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ResetDnsViaNetsh(string adapterName)
        {
            try
            {
                string safeName = adapterName.Replace("\"", "\\\"");
                string cmd = $"interface ipv4 set dns name=\"{safeName}\" source=dhcp";
                bool ok = RunProcess("netsh", cmd, out _, out _);

                string cmd6 = $"interface ipv6 set dnsservers name=\"{safeName}\" source=dhcp";
                RunProcess("netsh", cmd6, out _, out _);

                return ok;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> ConfigureDoHAsync(NetworkAdapterInfo adapter, string dnsIp, string dohTemplate)
        {
            try
            {
                string safeName = adapter.Name.Replace("\"", "\\\"");
                string cmd = $"dns add encryption interface=\"{safeName}\" ip={dnsIp.Trim()} dohtemplate=\"{dohTemplate.Trim()}\" autoupgrade=yes";
                return await Task.Run(() => RunProcess("netsh", cmd, out _, out _));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWindows11OrGreater()
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

        internal static async Task<(bool Success, string Output)> RunProcessAsync(
            string filename,
            IEnumerable<string> arguments,
            int timeoutMs = 5000,
            CancellationToken ct = default)
        {
            try
            {
                var psi = new ProcessStartInfo(filename)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                foreach (var arg in arguments)
                {
                    psi.ArgumentList.Add(arg);
                }

                using var proc = new Process { StartInfo = psi };
                if (!proc.Start())
                {
                    return (false, $"Could not start {filename}");
                }

                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                try
                {
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync(linkedCts.Token);
                    var stderrTask = proc.StandardError.ReadToEndAsync(linkedCts.Token);

                    await proc.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

                    string output = await stdoutTask.ConfigureAwait(false);
                    string error = await stderrTask.ConfigureAwait(false);

                    bool success = proc.ExitCode == 0;
                    string result = success
                        ? (string.IsNullOrWhiteSpace(output) ? error : output)
                        : (string.IsNullOrWhiteSpace(error) ? output : error);
                    return (success, result.Trim());
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Ignore error if process already exited
                    }

                    string reason = timeoutCts.IsCancellationRequested ? "timed out" : "cancelled";
                    return (false, $"Process '{filename}' {reason}.");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        internal static async Task<(bool Success, string Output)> RunProcessAsync(
            string filename,
            string arguments,
            int timeoutMs = 5000,
            CancellationToken ct = default)
        {
            try
            {
                var psi = new ProcessStartInfo(filename, arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = new Process { StartInfo = psi };
                if (!proc.Start())
                {
                    return (false, $"Could not start {filename}");
                }

                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                try
                {
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync(linkedCts.Token);
                    var stderrTask = proc.StandardError.ReadToEndAsync(linkedCts.Token);

                    await proc.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

                    string output = await stdoutTask.ConfigureAwait(false);
                    string error = await stderrTask.ConfigureAwait(false);

                    bool success = proc.ExitCode == 0;
                    string result = success
                        ? (string.IsNullOrWhiteSpace(output) ? error : output)
                        : (string.IsNullOrWhiteSpace(error) ? output : error);
                    return (success, result.Trim());
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Ignore error if process already exited
                    }

                    string reason = timeoutCts.IsCancellationRequested ? "timed out" : "cancelled";
                    return (false, $"Process '{filename}' {reason}.");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        internal static async Task<(bool Success, string Output)> RunPowerShellAsync(
            string command,
            int timeoutMs = 5000,
            CancellationToken ct = default)
        {
            // Suppress progress stream and encode as UTF-16LE Base64 (-EncodedCommand) to eliminate quoting/injection risks
            string script = $"$ProgressPreference = 'SilentlyContinue'; {command}";
            byte[] bytes = Encoding.Unicode.GetBytes(script);
            string base64 = Convert.ToBase64String(bytes);

            return await RunProcessAsync(
                "powershell.exe",
                new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", base64 },
                timeoutMs,
                ct).ConfigureAwait(false);
        }

        private static bool RunProcess(string filename, string arguments, out string output, out string error)
        {
            try
            {
                var task = RunProcessAsync(filename, arguments, timeoutMs: 4000);
                var (success, result) = task.GetAwaiter().GetResult();
                output = result;
                error = success ? string.Empty : result;
                return success;
            }
            catch (Exception ex)
            {
                output = string.Empty;
                error = ex.Message;
                return false;
            }
        }
    }
}
