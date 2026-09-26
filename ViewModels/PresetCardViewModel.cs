using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyDNS.Core.Interfaces;
using EasyDNS.Core.Models;

namespace EasyDNS.ViewModels
{
    public partial class PresetCardViewModel : ObservableObject
    {
        private readonly ILatencyBenchmarkService _benchmarkService;
        private readonly Action<DnsPreset> _onApply;
        private readonly Action<DnsPreset> _onDelete;
        private readonly Action<string> _onIpCopied;
        private readonly Action<DnsPreset> _onCardSelected;

        public DnsPreset Preset { get; }

        public string Id => Preset.Id;
        public string Name => Preset.Name;
        public string PrimaryDns => Preset.PrimaryDns;
        public string SecondaryDns => Preset.SecondaryDns;
        public string? PrimaryIpv6 => Preset.PrimaryIpv6;
        public string? SecondaryIpv6 => Preset.SecondaryIpv6;
        public string Category => Preset.Category;
        public string Description => Preset.Description;
        public string Tag => Preset.Tag;
        public bool IsCustom => Preset.IsCustom;
        public string DohTemplate => Preset.DohTemplate;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isCheckingLatency;

        [ObservableProperty]
        private long? _latencyMs;

        [ObservableProperty]
        private string _latencyDisplayText = "● -- ms";

        [ObservableProperty]
        private Brush _latencyBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // TextMuted

        [ObservableProperty]
        private Brush _latencyBadgeBackground = new SolidColorBrush(Color.FromArgb(24, 100, 116, 139));

        [ObservableProperty]
        private Brush _latencyBadgeBorderBrush = new SolidColorBrush(Color.FromArgb(80, 100, 116, 139));

        [ObservableProperty]
        private ImageSource? _providerLogo;

        [ObservableProperty]
        private string _providerInitials = "DNS";

        [ObservableProperty]
        private Brush _avatarBackground = new SolidColorBrush(Color.FromArgb(28, 99, 102, 241));

        [ObservableProperty]
        private Brush _avatarBorderBrush = new SolidColorBrush(Color.FromArgb(90, 99, 102, 241));

        public bool HasSecondaryDns => !string.IsNullOrWhiteSpace(SecondaryDns);

        public PresetCardViewModel(
            DnsPreset preset,
            ILatencyBenchmarkService benchmarkService,
            Action<DnsPreset> onApply,
            Action<DnsPreset> onDelete,
            Action<string> onIpCopied,
            Action<DnsPreset> onCardSelected)
        {
            Preset = preset;
            _benchmarkService = benchmarkService;
            _onApply = onApply;
            _onDelete = onDelete;
            _onIpCopied = onIpCopied;
            _onCardSelected = onCardSelected;

            LoadLogo();
            InitBrandColors();
        }

        private void InitBrandColors()
        {
            var brandColor = GetProviderColor(Preset);
            AvatarBackground = new SolidColorBrush(Color.FromArgb(28, brandColor.R, brandColor.G, brandColor.B));
            AvatarBorderBrush = new SolidColorBrush(Color.FromArgb(90, brandColor.R, brandColor.G, brandColor.B));
        }

        private static Color GetProviderColor(DnsPreset preset)
        {
            string key = GetProviderKey(preset);
            return key switch
            {
                "cloudflare" => Color.FromRgb(246, 130, 31),
                "google" => Color.FromRgb(66, 133, 244),
                "quad9" => Color.FromRgb(139, 92, 246),
                "adguard" => Color.FromRgb(16, 185, 129),
                "opendns" => Color.FromRgb(6, 182, 212),
                "controld" => Color.FromRgb(236, 72, 153),
                "cleanbrowsing" => Color.FromRgb(59, 130, 246),
                "nextdns" => Color.FromRgb(99, 102, 241),
                "shecan" => Color.FromRgb(245, 158, 11),
                "electro" => Color.FromRgb(168, 85, 247),
                "403" => Color.FromRgb(6, 182, 212),
                "radar" => Color.FromRgb(16, 185, 129),
                "dnswatch" => Color.FromRgb(20, 184, 166),
                "level3" => Color.FromRgb(14, 165, 233),
                "comodo" => Color.FromRgb(239, 68, 68),
                _ => Color.FromRgb(99, 102, 241)
            };
        }

        private void LoadLogo()
        {
            string key = GetProviderKey(Preset);
            try
            {
                var uri = new Uri($"pack://application:,,,/Resources/Logos/{key}.png", UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                ProviderLogo = bitmap;
            }
            catch
            {
                ProviderLogo = null;
                ProviderInitials = GetInitials(Preset.Name);
            }
        }

        private static string GetProviderKey(DnsPreset preset)
        {
            if (preset.IsCustom || string.IsNullOrEmpty(preset.Name)) return "custom";
            string name = preset.Name.ToUpperInvariant();
            if (name.StartsWith("CLOUDFLARE")) return "cloudflare";
            if (name.StartsWith("GOOGLE")) return "google";
            if (name.StartsWith("QUAD9")) return "quad9";
            if (name.StartsWith("ADGUARD")) return "adguard";
            if (name.StartsWith("OPENDNS")) return "opendns";
            if (name.StartsWith("CONTROL D")) return "controld";
            if (name.StartsWith("CLEANBROWSING")) return "cleanbrowsing";
            if (name.StartsWith("NEXTDNS")) return "nextdns";
            if (name.StartsWith("SHECAN")) return "shecan";
            if (name.StartsWith("ELECTRO")) return "electro";
            if (name.StartsWith("403")) return "403";
            if (name.StartsWith("RADAR")) return "radar";
            if (name.StartsWith("LEVEL3")) return "level3";
            if (name.StartsWith("COMODO")) return "comodo";
            if (name.StartsWith("DNS.WATCH")) return "dnswatch";
            return "custom";
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "DNS";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }

        public void UpdateLatency(long? rttMs, bool checking)
        {
            IsCheckingLatency = checking;
            LatencyMs = rttMs;

            Color c;
            if (checking)
            {
                LatencyDisplayText = "● Ping...";
                c = Color.FromRgb(245, 158, 11); // Amber AccentWarning
            }
            else if (!rttMs.HasValue)
            {
                LatencyDisplayText = "● -- ms";
                c = Color.FromRgb(100, 116, 139); // TextMuted
            }
            else if (rttMs.Value < 0)
            {
                LatencyDisplayText = "● Timeout";
                c = Color.FromRgb(244, 63, 94); // AccentDanger
            }
            else
            {
                LatencyDisplayText = $"● {rttMs.Value} ms";
                if (rttMs.Value <= 45)
                    c = Color.FromRgb(16, 185, 129); // AccentSuccess
                else if (rttMs.Value <= 100)
                    c = Color.FromRgb(245, 158, 11); // AccentWarning
                else
                    c = Color.FromRgb(244, 63, 94); // AccentDanger
            }

            LatencyBrush = new SolidColorBrush(c);
            LatencyBadgeBackground = new SolidColorBrush(Color.FromArgb(24, c.R, c.G, c.B));
            LatencyBadgeBorderBrush = new SolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B));
        }

        [RelayCommand]
        public void Apply() => _onApply(Preset);

        [RelayCommand]
        public void SelectCard() => _onCardSelected(Preset);

        [RelayCommand]
        public async Task TestLatencyAsync()
        {
            UpdateLatency(null, true);
            var result = await _benchmarkService.MeasurePresetLatencyAsync(Preset);
            UpdateLatency(result.Success ? result.RoundtripTimeMs : -1, false);
        }

        [RelayCommand]
        public void Delete() => _onDelete(Preset);

        [RelayCommand]
        public void CopyPrimaryIp()
        {
            try
            {
                Clipboard.SetText(PrimaryDns);
                _onIpCopied(PrimaryDns);
            }
            catch { /* Ignore clipboard access error */ }
        }

        [RelayCommand]
        public void CopySecondaryIp()
        {
            if (string.IsNullOrEmpty(SecondaryDns)) return;
            try
            {
                Clipboard.SetText(SecondaryDns);
                _onIpCopied(SecondaryDns);
            }
            catch { /* Ignore clipboard access error */ }
        }
    }
}
