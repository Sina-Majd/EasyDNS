# EasyDNS

EasyDNS is a lightweight Windows desktop utility for configuring network adapter DNS settings. It provides quick switching between popular DNS resolvers, custom DNS management, real-time latency benchmarking, and cache clearing.

## Features

- **DNS Presets**: Preconfigured profiles for major public DNS providers (Cloudflare, Google Public DNS, Quad9, OpenDNS, AdGuard, Control D, NextDNS, and regional/gaming resolvers).
- **Custom DNS Profiles**: Add, save, and manage custom IPv4 primary and secondary DNS server pairs.
- **Latency Benchmarking**: Measure response times across all resolvers in parallel to identify the fastest DNS server for your network.
- **DNS Cache Flushing**: Flush the Windows DNS resolver cache directly via native Windows APIs and `ipconfig`.
- **DHCP Restoration**: Restore network adapter DNS settings to automatic (DHCP) with one click.
- **Adapter Detection**: Automatically detects active physical and wireless network adapters.

## System Requirements

- **Operating System**: Windows 10 or Windows 11 (Windows 7/8 supported with .NET 4.8)
- **Runtime**: .NET Framework 4.8 (pre-installed on modern Windows versions)
- **Permissions**: Administrator privileges (required by Windows to modify network interface configurations)

## Usage

1. Download the latest `EasyDNS.exe` from the Releases section.
2. Launch `EasyDNS.exe` and grant administrative permissions when prompted by UAC.
3. Select the target network adapter from the adapter list.
4. Select a preset or enter custom DNS addresses and click **Apply DNS**.

## Building from Source

### Prerequisites

- Visual Studio 2019 / 2022 (with the **.NET desktop development** workload) or the standalone MSBuild Tools.
- .NET Framework 4.8 Developer Pack.

### Build Instructions

To compile from the command line using MSBuild:

```cmd
msbuild EasyDNS.csproj /p:Configuration=Release
```

Alternatively, run the included build script:

```cmd
build.bat
```

The compiled standalone executable will be output to:
```
bin\Release\EasyDNS.exe
```

## Storage & Configuration

Custom DNS presets are stored locally as XML in the user's application data directory:
```
%LocalAppData%\EasyDNS\custom_presets.xml
```

## Technical Implementation

- **DNS Configuration**: Uses Windows Management Instrumentation (WMI via `Win32_NetworkAdapterConfiguration`) with fallback to `netsh interface ipv4`.
- **Cache Clearing**: Invokes `DnsFlushResolverCache` from `dnsapi.dll` and `ipconfig /flushdns`.
- **Latency Testing**: Asynchronous ICMP ping requests through `System.Net.NetworkInformation.Ping`.

## License

This project is licensed under the MIT License.
