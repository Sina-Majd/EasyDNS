using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using EasyDNS.ViewModels;
using Forms = System.Windows.Forms;

namespace EasyDNS.Views
{
    public class CategoryCheckedConverter : IValueConverter
    {
        public static CategoryCheckedConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string current && parameter is string target)
            {
                return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string target)
            {
                return target;
            }
            return Binding.DoNothing;
        }
    }

    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private readonly MainViewModel _viewModel;
        private Forms.NotifyIcon? _trayIcon;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ShowMessageBoxHandler = (msg, title, btn, img) => DarkMessageBox.Show(this, msg, title, btn, img);
            _viewModel.ShowPromptDialogHandler = (title, prompt, def) => PromptDialog.Show(this, title, prompt, def);

            _viewModel.Logger.OnLogAdded += () =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    LogScrollViewer.ScrollToEnd();
                });
            };

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            try
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch
            {
                // Silently skip if OS does not support DWM corner preference
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeSystemTray();
        }

        private void InitializeSystemTray()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(iconUri);
                Icon? appIcon = null;
                if (streamInfo != null)
                {
                    appIcon = new Icon(streamInfo.Stream);
                }

                _trayIcon = new Forms.NotifyIcon
                {
                    Icon = appIcon ?? SystemIcons.Application,
                    Text = "EasyDNS - Network DNS Utility",
                    Visible = true
                };

                var contextMenu = new Forms.ContextMenuStrip();
                contextMenu.Items.Add("Open EasyDNS", null, delegate { RestoreFromTray(); });
                contextMenu.Items.Add(new Forms.ToolStripSeparator());

                var quickPresets = new Forms.ToolStripMenuItem("Quick Presets");
                string[] quickNames = { "Cloudflare (1.1.1.1)", "Google Public DNS", "Shecan", "Electro DNS (Gaming)", "Radar Game" };
                foreach (var qName in quickNames)
                {
                    var targetName = qName;
                    quickPresets.DropDownItems.Add(targetName, null, delegate
                    {
                        var match = _viewModel.FilteredPresets.FirstOrDefault(p => p.Name == targetName);
                        if (match != null)
                        {
                            _viewModel.ApplyPreset(match.Preset);
                        }
                    });
                }
                contextMenu.Items.Add(quickPresets);

                contextMenu.Items.Add("Restore Default (DHCP)", null, delegate { _ = _viewModel.ResetDhcpAsync(); });
                contextMenu.Items.Add("Flush DNS Cache", null, delegate { _ = _viewModel.FlushDnsAsync(); });
                contextMenu.Items.Add(new Forms.ToolStripSeparator());
                contextMenu.Items.Add("Exit", null, delegate
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                    Application.Current.Shutdown();
                });

                _trayIcon.ContextMenuStrip = contextMenu;
                _trayIcon.DoubleClick += delegate { RestoreFromTray(); };
            }
            catch
            {
                // Graceful fallback if notify icon fails
            }
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }

        private void CategoryPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string cat)
            {
                _viewModel.SelectedCategory = cat;
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            Hide();
            _trayIcon?.ShowBalloonTip(1500, "EasyDNS", "EasyDNS is minimized to tray. Double-click icon to restore.", Forms.ToolTipIcon.Info);
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateMaximizeState();
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            UpdateMaximizeState();
        }

        private void UpdateMaximizeState()
        {
            bool isMaximized = WindowState == WindowState.Maximized;
            GlyphMaximize.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
            GlyphRestore.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
            BtnMaximize.ToolTip = isMaximized ? "Restore" : "Maximize";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
