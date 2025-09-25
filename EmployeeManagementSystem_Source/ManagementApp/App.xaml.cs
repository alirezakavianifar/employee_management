using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace ManagementApp
{
    public partial class App : System.Windows.Application
    {
        private static readonly ILogger<App> Logger = LoggingService.CreateLogger<App>();

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Setup Georgian culture and LTR support
                SetupGeorgianCulture();
                
                // Setup logging
                LoggingService.ConfigureLogging();
                Logger.LogInformation("Starting Management Application");
                
                // Set RTL flow direction for the entire application
                // FlowDirection = System.Windows.FlowDirection.RightToLeft;
                
                base.OnStartup(e);
                
                Logger.LogInformation("Management Application started successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to start Management Application");
                MessageBox.Show($"خطا در راه‌اندازی برنامه: {ex.Message}", "خطا", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void SetupGeorgianCulture()
        {
            try
            {
                // Set Georgian culture for the application
                var georgianCulture = new CultureInfo("en-US");
                CultureInfo.DefaultThreadCurrentCulture = georgianCulture;
                CultureInfo.DefaultThreadCurrentUICulture = georgianCulture;
                Thread.CurrentThread.CurrentCulture = georgianCulture;
                Thread.CurrentThread.CurrentUICulture = georgianCulture;
                
                Logger.LogInformation("Georgian culture setup completed");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to setup Georgian culture, using default");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Logger.LogInformation("Shutting down Management Application");
                base.OnExit(e);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during application shutdown");
            }
        }
    }
}
