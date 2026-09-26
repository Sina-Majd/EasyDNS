using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyDNS.Core.Common;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;
using Microsoft.Win32;

namespace EasyDNS.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INetworkDnsService _dnsService;
        private readonly ILatencyBenchmarkService _benchmarkService;
        private readonly IPresetRepository _presetRepo;
        public IAppLogger Logger { get; }

        public Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult>? ShowMessageBoxHandler { get; set; }
        public Func<string, string, string, string?>? ShowPromptDialogHandler { get; set; }

        [ObservableProperty]
        private ObservableCollection<NetworkAdapterInfo> _adapters = new();

        [ObservableProperty]
        private NetworkAdapterInfo? _selectedAdapter;

        [ObservableProperty]
        private string _currentPrimaryDns = "Primary: None";

        [ObservableProperty]
        private string _currentSecondaryDns = "Secondary: None";

        [ObservableProperty]
        private string _currentLatencyText = "● Latency: --";

        [ObservableProperty]
        private Brush _currentLatencyBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<PresetCardViewModel> _filteredPresets = new();

        private readonly List<PresetCardViewModel> _allPresetViewModels = new();

        [ObservableProperty]
        private string _inputPrimaryDns = string.Empty;

        [ObservableProperty]
        private string _inputSecondaryDns = string.Empty;

        [ObservableProperty]
        private bool _isBenchmarkingAll;

        [ObservableProperty]
        private string _benchmarkButtonText = "Ping All";

        [ObservableProperty]
        private Brush _benchmarkButtonBrush = new SolidColorBrush(Color.FromRgb(79, 70, 229)); // Indigo

        private CancellationTokenSource? _benchmarkCts;
        private DnsPreset? _lastAppliedPreset;

        public MainViewModel(
            INetworkDnsService dnsService,
            ILatencyBenchmarkService benchmarkService,
            IPresetRepository presetRepo,
            IAppLogger logger)
        {
            _dnsService = dnsService;
            _benchmarkService = benchmarkService;
            _presetRepo = presetRepo;
            Logger = logger;

            Categories = new ObservableCollection<string>(_presetRepo.GetCategories());
            SelectedCategory = "All";

            LoadAdapters();
            ReloadPresets();
            Logger.LogSuccess("EasyDNS initialized successfully.");
        }

        partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
        {
            UpdateCurrentConfigView();
            if (value != null)
            {
                Logger.Log($"Selected adapter: {value.Name}");
            }
        }

        partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
        partial void OnSearchQueryChanged(string value) => ApplyFilters();

        public void LoadAdapters()
        {
            var list = _dnsService.GetNetworkAdapters();
            Adapters.Clear();
            foreach (var a in list)
            {
                Adapters.Add(a);
            }

            if (Adapters.Count > 0)
            {
                var operational = Adapters.FirstOrDefault(a => a.IsOperational) ?? Adapters[0];
                SelectedAdapter = operational;
            }
            else
            {
                SelectedAdapter = null;
            }

            UpdateCurrentConfigView();
        }

        private void UpdateCurrentConfigView()
        {
            if (SelectedAdapter == null)
            {
                CurrentPrimaryDns = "None";
                CurrentSecondaryDns = "None";
                CurrentLatencyText = "● Latency: -- ms";
                CurrentLatencyBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                UpdatePresetsActiveState(null);
                return;
            }

            if (SelectedAdapter.IsDnsAutomatic)
            {
                string routerDns = SelectedAdapter.DnsServers.Count > 0 ? SelectedAdapter.DnsServers[0] : string.Empty;
                CurrentPrimaryDns = !string.IsNullOrEmpty(routerDns)
                    ? $"Automatic ({routerDns})"
                    : "Automatic (Default)";
                CurrentSecondaryDns = SelectedAdapter.DnsServers.Count > 1
                    ? $"Automatic ({SelectedAdapter.DnsServers[1]})"
                    : "None";
                UpdatePresetsActiveState(null);
            }
            else if (SelectedAdapter.DnsServers.Count > 0)
            {
                CurrentPrimaryDns = SelectedAdapter.DnsServers[0];
                CurrentSecondaryDns = SelectedAdapter.DnsServers.Count > 1 ? SelectedAdapter.DnsServers[1] : "None";
                UpdatePresetsActiveState(SelectedAdapter.DnsServers[0]);
            }
            else
            {
                CurrentPrimaryDns = "Automatic (Default)";
                CurrentSecondaryDns = "None";
                UpdatePresetsActiveState(null);
            }

            CurrentLatencyText = "● Latency: -- ms";
            CurrentLatencyBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }

        private void UpdatePresetsActiveState(string? activePrimaryIp)
        {
            foreach (var vm in _allPresetViewModels)
            {
                vm.IsActive = !string.IsNullOrEmpty(activePrimaryIp) &&
                              string.Equals(vm.PrimaryDns, activePrimaryIp, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void ReloadPresets()
        {
            _allPresetViewModels.Clear();
            var presets = _presetRepo.GetAllPresets();

            foreach (var p in presets)
            {
                var vm = new PresetCardViewModel(
                    p,
                    _benchmarkService,
                    onApply: ApplyPreset,
                    onDelete: DeletePreset,
                    onIpCopied: ip => Logger.Log($"Copied DNS IP to clipboard: {ip}"),
                    onCardSelected: SelectPresetToInputs);

                _allPresetViewModels.Add(vm);
            }

            if (SelectedAdapter != null && SelectedAdapter.DnsServers.Count > 0)
            {
                UpdatePresetsActiveState(SelectedAdapter.DnsServers[0]);
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var q = SearchQuery.Trim().ToLowerInvariant();
            var cat = SelectedCategory;

            FilteredPresets.Clear();
            foreach (var vm in _allPresetViewModels)
            {
                bool catMatch = cat == "All" || (!string.IsNullOrEmpty(vm.Category) && vm.Category.Contains(cat, StringComparison.OrdinalIgnoreCase));
                bool searchMatch = string.IsNullOrEmpty(q) ||
                                   vm.Name.ToLowerInvariant().Contains(q) ||
                                   vm.PrimaryDns.Contains(q) ||
                                   vm.SecondaryDns.Contains(q) ||
                                   vm.Description.ToLowerInvariant().Contains(q);

                if (catMatch && searchMatch)
                {
                    FilteredPresets.Add(vm);
                }
            }
        }

        private void SelectPresetToInputs(DnsPreset preset)
        {
            InputPrimaryDns = preset.PrimaryDns;
            InputSecondaryDns = preset.SecondaryDns;
            _lastAppliedPreset = preset;
            Logger.Log($"Loaded {preset.Name} into inputs.");
        }

        public void ApplyPreset(DnsPreset preset)
        {
            _lastAppliedPreset = preset;
            InputPrimaryDns = preset.PrimaryDns;
            InputSecondaryDns = preset.SecondaryDns;
            _ = ApplyInputsDnsAsync();
        }

        private void DeletePreset(DnsPreset preset)
        {
            var result = ShowMessageBox(
                $"Delete custom preset '{preset.Name}'?",
                "Delete Preset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _ = DeletePresetAsync(preset.Id, preset.Name);
            }
        }

        private async Task DeletePresetAsync(string id, string name)
        {
            await _presetRepo.DeleteCustomPresetAsync(id);
            ReloadPresets();
            Logger.LogSuccess($"Deleted custom preset: {name}");
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshAdaptersCommand))]
        private bool _isRefreshingAdapters;

        private bool CanRefreshAdapters => !IsRefreshingAdapters;

        [RelayCommand(CanExecute = nameof(CanRefreshAdapters))]
        public async Task RefreshAdaptersAsync()
        {
            IsRefreshingAdapters = true;
            try
            {
                var minSpinTask = Task.Delay(500);
                var listTask = Task.Run(() => _dnsService.GetNetworkAdapters());
                await Task.WhenAll(minSpinTask, listTask);

                var list = await listTask;
                Adapters.Clear();
                foreach (var a in list)
                {
                    Adapters.Add(a);
                }

                if (Adapters.Count > 0)
                {
                    var operational = Adapters.FirstOrDefault(a => a.IsOperational) ?? Adapters[0];
                    SelectedAdapter = operational;
                }
                else
                {
                    SelectedAdapter = null;
                }

                UpdateCurrentConfigView();
                Logger.Log("Refreshed network adapters.");
            }
            finally
            {
                IsRefreshingAdapters = false;
            }
        }

        [RelayCommand]
        public async Task TestCurrentDnsLatencyAsync()
        {
            if (SelectedAdapter == null || SelectedAdapter.DnsServers.Count == 0)
            {
                CurrentLatencyText = "● Latency: -- ms";
                CurrentLatencyBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                return;
            }

            string target = SelectedAdapter.DnsServers[0];
            CurrentLatencyText = "● Latency: Pinging...";
            CurrentLatencyBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));

            var result = await _benchmarkService.MeasureLatencyAsync(target);
            if (result.Success)
            {
                CurrentLatencyText = $"● Latency: {result.RoundtripTimeMs} ms";
                CurrentLatencyBrush = GetLatencyBrush(result.RoundtripTimeMs);
                Logger.LogHighlight($"Current DNS ({target}) latency: {result.RoundtripTimeMs} ms [{result.Protocol}]");
            }
            else
            {
                CurrentLatencyText = "● Latency: Timeout";
                CurrentLatencyBrush = new SolidColorBrush(Color.FromRgb(244, 63, 94));
                Logger.LogWarning($"Current DNS ({target}) ping timed out.");
            }
        }

        private static Brush GetLatencyBrush(long rttMs)
        {
            if (rttMs <= 45) return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Emerald Green
            if (rttMs <= 100) return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
            return new SolidColorBrush(Color.FromRgb(244, 63, 94)); // Rose Red
        }

        [RelayCommand]
        public async Task BenchmarkAllAsync()
        {
            if (IsBenchmarkingAll)
            {
                _benchmarkCts?.Cancel();
                Logger.Log("Cancelling latency benchmark...");
                return;
            }

            IsBenchmarkingAll = true;
            BenchmarkButtonText = "Cancel";
            BenchmarkButtonBrush = new SolidColorBrush(Color.FromRgb(244, 63, 94)); // Red
            _benchmarkCts = new CancellationTokenSource();
            var token = _benchmarkCts.Token;

            Logger.LogHighlight("Starting parallel latency benchmark (UDP 53 with ICMP fallback)...");

            foreach (var vm in _allPresetViewModels)
            {
                vm.UpdateLatency(null, true);
            }

            try
            {
                var presets = _allPresetViewModels.Select(v => v.Preset).ToList();
                await _benchmarkService.BenchmarkAllAsync(presets, (preset, res) =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var match = _allPresetViewModels.FirstOrDefault(v => v.Id == preset.Id);
                        match?.UpdateLatency(res.Success ? res.RoundtripTimeMs : -1, false);
                    });
                }, token);
            }
            catch (OperationCanceledException)
            {
                // Cancelled
            }
            finally
            {
                IsBenchmarkingAll = false;
                BenchmarkButtonText = "Ping All";
                BenchmarkButtonBrush = new SolidColorBrush(Color.FromRgb(79, 70, 229)); // Indigo
                _benchmarkCts?.Dispose();
                _benchmarkCts = null;

                foreach (var vm in _allPresetViewModels)
                {
                    if (vm.IsCheckingLatency)
                    {
                        vm.UpdateLatency(vm.LatencyMs, false);
                    }
                }

                if (token.IsCancellationRequested)
                    Logger.Log("Benchmark cancelled by user.");
                else
                    Logger.LogSuccess("Benchmark completed across all resolvers.");
            }
        }

        [RelayCommand]
        public async Task SelectFastestDnsAsync()
        {
            Logger.Log("Finding fastest DNS server...");
            await BenchmarkAllAsync();

            var fastest = _allPresetViewModels
                .Where(v => v.LatencyMs.HasValue && v.LatencyMs.Value > 0)
                .OrderBy(v => v.LatencyMs!.Value)
                .FirstOrDefault();

            if (fastest != null)
            {
                InputPrimaryDns = fastest.PrimaryDns;
                InputSecondaryDns = fastest.SecondaryDns;
                _lastAppliedPreset = fastest.Preset;

                string msg = $"Fastest DNS: {fastest.Name} ({fastest.LatencyMs} ms)\n\nWould you like to apply it now to '{SelectedAdapter?.Name}'?";
                if (ShowMessageBox(msg, "Fastest DNS Found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    await ApplyInputsDnsAsync();
                }
            }
            else
            {
                ShowMessageBox("No responsive DNS servers were detected in the benchmark.", "EasyDNS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        public async Task ApplyInputsDnsAsync()
        {
            if (SelectedAdapter == null)
            {
                ShowMessageBox("Please select a valid network adapter first.", "EasyDNS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string primary = InputPrimaryDns.Trim();
            string secondary = InputSecondaryDns.Trim();

            if (!IpValidator.IsValidIp(primary))
            {
                ShowMessageBox("Please enter a valid Primary DNS address (IPv4 or IPv6).", "Invalid IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(secondary) && !IpValidator.IsValidIp(secondary))
            {
                ShowMessageBox("Please enter a valid Secondary DNS address (IPv4 or IPv6) or leave it blank.", "Invalid IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Logger.Log($"Applying DNS ({primary}{(string.IsNullOrEmpty(secondary) ? "" : $", {secondary}")}) to {SelectedAdapter.Name}...");

            string? doh = (_lastAppliedPreset != null && string.Equals(_lastAppliedPreset.PrimaryDns, primary, StringComparison.OrdinalIgnoreCase))
                ? _lastAppliedPreset.DohTemplate
                : null;

            string? ipv6_1 = (_lastAppliedPreset != null && string.Equals(_lastAppliedPreset.PrimaryDns, primary, StringComparison.OrdinalIgnoreCase))
                ? _lastAppliedPreset.PrimaryIpv6
                : null;

            string? ipv6_2 = (_lastAppliedPreset != null && string.Equals(_lastAppliedPreset.PrimaryDns, primary, StringComparison.OrdinalIgnoreCase))
                ? _lastAppliedPreset.SecondaryIpv6
                : null;

            var result = await _dnsService.SetDnsAsync(SelectedAdapter, primary, secondary, doh, ipv6_1, ipv6_2);
            if (result.Success)
            {
                Logger.LogSuccess($"OK: {result.Message}");
                LoadAdapters();
            }
            else
            {
                Logger.LogError($"FAIL: {result.Message}");
                ShowMessageBox(result.Message, "EasyDNS Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task ResetDhcpAsync()
        {
            if (SelectedAdapter == null)
            {
                ShowMessageBox("Please select a valid network adapter first.", "EasyDNS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Logger.Log($"Resetting DNS to default on {SelectedAdapter.Name}...");
            var result = await _dnsService.ResetToDhcpAsync(SelectedAdapter);

            if (result.Success)
            {
                Logger.LogSuccess($"OK: {result.Message}");
                InputPrimaryDns = string.Empty;
                InputSecondaryDns = string.Empty;
                LoadAdapters();
            }
            else
            {
                Logger.LogError($"FAIL: {result.Message}");
                ShowMessageBox(result.Message, "EasyDNS Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task FlushDnsAsync()
        {
            Logger.Log("Flushing Windows DNS Resolver Cache...");
            var result = await _dnsService.FlushDnsCacheAsync();

            if (result.Success)
            {
                Logger.LogSuccess($"OK: {result.Message}");
                ShowMessageBox("Windows DNS Resolver Cache successfully flushed.", "Flush DNS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Logger.LogError($"FAIL: {result.Message}");
            }
        }

        [RelayCommand]
        public void SwapInputs()
        {
            (InputPrimaryDns, InputSecondaryDns) = (InputSecondaryDns, InputPrimaryDns);
            Logger.Log("Swapped Primary and Secondary DNS inputs.");
        }

        [RelayCommand]
        public void ClearInputs()
        {
            InputPrimaryDns = string.Empty;
            InputSecondaryDns = string.Empty;
            _lastAppliedPreset = null;
        }

        [RelayCommand]
        public async Task SaveCustomPresetAsync()
        {
            string primary = InputPrimaryDns.Trim();
            string secondary = InputSecondaryDns.Trim();

            if (!IpValidator.IsValidIp(primary))
            {
                ShowMessageBox("Please enter a valid Primary DNS to save preset.", "Invalid IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? name = ShowPromptDialog("Save Custom Preset", "Enter a name for this custom DNS preset:", "My Custom DNS");
            if (string.IsNullOrWhiteSpace(name)) return;

            await _presetRepo.AddCustomPresetAsync(name.Trim(), primary, secondary, "Custom DNS preset saved by user.");
            ReloadPresets();
            Logger.LogSuccess($"Saved custom preset: {name}");
            ShowMessageBox($"Preset '{name}' saved successfully!", "EasyDNS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public async Task ExportPresetsAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml",
                FileName = "EasyDNS_Presets.json",
                Title = "Export Custom DNS Presets"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = await _presetRepo.ExportCustomPresetsAsync(dialog.FileName);
                if (result.Success)
                {
                    Logger.LogSuccess(result.Message);
                    ShowMessageBox(result.Message, "Export Presets", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Logger.LogError(result.Message);
                    ShowMessageBox(result.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        public async Task ImportPresetsAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Presets Files (*.json;*.xml)|*.json;*.xml|All Files (*.*)|*.*",
                Title = "Import Custom DNS Presets"
            };

            if (dialog.ShowDialog() == true)
            {
                var (result, count) = await _presetRepo.ImportCustomPresetsAsync(dialog.FileName);
                if (result.Success)
                {
                    ReloadPresets();
                    Logger.LogSuccess(result.Message);
                    ShowMessageBox(result.Message, "Import Presets", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Logger.LogWarning(result.Message);
                    ShowMessageBox(result.Message, "Import Presets", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        public void ClearLogs() => Logger.Clear();

        private MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            if (ShowMessageBoxHandler != null)
            {
                return ShowMessageBoxHandler(message, title, buttons, icon);
            }
            return MessageBox.Show(message, title, buttons, icon);
        }

        private string? ShowPromptDialog(string title, string prompt, string defaultValue)
        {
            if (ShowPromptDialogHandler != null)
            {
                return ShowPromptDialogHandler(title, prompt, defaultValue);
            }
            return null;
        }
    }
}
