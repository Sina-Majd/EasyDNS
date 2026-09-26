using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using EasyDNS.Core.Interfaces;
using EasyDNS.Infrastructure.Services;
using EasyDNS.ViewModels;
using EasyDNS.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EasyDNS
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configure Global Exception Handling
            DispatcherUnhandledException += (s, args) =>
            {
                DarkMessageBox.Show(null, $"An unexpected error occurred: {args.Exception.Message}", "EasyDNS Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    DarkMessageBox.Show(null, $"Fatal error: {ex.Message}", "EasyDNS Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Administrator Elevation Check
            if (!IsRunAsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "EasyDNS.exe",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    Shutdown();
                    return;
                }
                catch
                {
                    DarkMessageBox.Show(null, "EasyDNS requires Administrator privileges to modify network and DNS settings.\nPlease allow the Administrator prompt to run.", "EasyDNS - Administrator Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }
            }

            // Dependency Injection Configuration
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Show Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Core & Infrastructure
            services.AddSingleton<IAppLogger, AppLogger>();
            services.AddSingleton<INetworkDnsService, WindowsIpHelperDnsService>();
            services.AddSingleton<ILatencyBenchmarkService, LatencyBenchmarkService>();
            services.AddSingleton<IPresetRepository, JsonPresetRepository>();

            // ViewModels
            services.AddSingleton<MainViewModel>();

            // Views
            services.AddTransient<MainWindow>();
        }

        private static bool IsRunAsAdmin()
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
    }
}
