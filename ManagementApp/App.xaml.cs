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
                Logger.LogInformation("Georgian culture setup completed");
                
                // Set up global exception handling to catch silent failures
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;
                
                // Set RTL flow direction for the entire application
                // FlowDirection = System.Windows.FlowDirection.RightToLeft;
                
                base.OnStartup(e);
                
                // Explicitly create and show MainWindow for better error handling
                try
                {
                    var mainWindow = new Views.MainWindow();
                    MainWindow = mainWindow; // Set as the main window
                    mainWindow.Show();
                    mainWindow.Activate(); // Bring window to front
                    mainWindow.Focus(); // Focus the window
                    Logger.LogInformation("MainWindow created and shown successfully");
                }
                catch (Exception windowEx)
                {
                    Logger?.LogError(windowEx, "Failed to create or show MainWindow");
                    var errorMessage = $"خطا در ایجاد پنجره اصلی:\n\n{windowEx.Message}\n\nجزئیات:\n{windowEx}";
                    MessageBox.Show(errorMessage, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }
                
                Logger.LogInformation("Management Application started successfully");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to start Management Application");
                var errorMessage = $"خطا در راه‌اندازی برنامه:\n\n{ex.Message}\n\nجزئیات:\n{ex}";
                MessageBox.Show(errorMessage, "خطا", 
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
                
                // Logging will be configured later, so don't log here
            }
            catch (Exception ex)
            {
                // Logging not configured yet, so just continue
                // Error will be logged after ConfigureLogging is called
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

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                Logger?.LogError(exception, "Unhandled exception occurred");
                
                var errorMessage = $"خطای غیرمنتظره:\n\n{exception?.Message}\n\nجزئیات:\n{exception}";
                MessageBox.Show(errorMessage, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("خطای غیرمنتظره رخ داده است", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Logger?.LogError(e.Exception, "Dispatcher unhandled exception occurred");
                
                var errorMessage = $"خطا در رابط کاربری:\n\n{e.Exception.Message}\n\nجزئیات:\n{e.Exception}";
                MessageBox.Show(errorMessage, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Mark as handled to prevent application crash, but log it
                e.Handled = true;
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("خطا در رابط کاربری رخ داده است", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        }
    }
}
